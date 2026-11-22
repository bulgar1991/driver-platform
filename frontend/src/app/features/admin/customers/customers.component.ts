import { DatePipe, DecimalPipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core'
import { takeUntilDestroyed } from '@angular/core/rxjs-interop'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { ActivatedRoute, Router, RouterLink } from '@angular/router'
import { MatCheckbox } from '@angular/material/checkbox'
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field'
import { MatInput } from '@angular/material/input'
import { MatOption, MatSelect } from '@angular/material/select'
import { Drawer } from 'primeng/drawer'
import { debounceTime, distinctUntilChanged, finalize, Subject } from 'rxjs'
import {
  CustomerDetail,
  CustomerFilter,
  CustomerList,
  CustomerQuery,
  CustomerSort,
} from '@/core/models'
import { AdminCustomersService } from '@/core/services/admin-customers.service'
import { ToastService } from '@/core/services/toast.service'
import { downloadCsv } from '@/core/utils/csv'
import { todayInOffice } from '@/core/utils/local-time'
import { problemMessage } from '@/core/utils/problem'
import {
  BOOKING_STATUS_LABEL,
  BOOKING_STATUS_TONE,
} from '@/core/utils/status-labels'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { ADMIN_TEXT } from '@/features/admin/admin-texts'
import { BookingDetailComponent } from '@/features/admin/bookings/booking-detail/booking-detail.component'

const PAGE_SIZE = 50

/**
 * Admin → Customers: everyone who booked (paid or through a partner), with rides, spend and the
 * last and next ride. A customer's card shows their history and keeps the contact details, the
 * language of their emails and the notes the drivers see on every ride.
 */
@Component({
  selector: 'app-customers',
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    RouterLink,
    MatCheckbox,
    MatError,
    MatFormField,
    MatLabel,
    MatInput,
    MatSelect,
    MatOption,
    Drawer,
    ButtonComponent,
    IconComponent,
    BookingDetailComponent,
  ],
  templateUrl: './customers.component.html',
  styleUrl: './customers.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomersComponent implements OnInit {
  protected readonly text = ADMIN_TEXT
  protected readonly statusLabel = BOOKING_STATUS_LABEL
  protected readonly statusTone = BOOKING_STATUS_TONE
  private readonly api = inject(AdminCustomersService)
  private readonly toast = inject(ToastService)
  private readonly router = inject(Router)
  private readonly route = inject(ActivatedRoute)
  private readonly fb = inject(FormBuilder).nonNullable
  private readonly destroyRef = inject(DestroyRef)

  protected readonly today = todayInOffice()
  protected readonly filters: { value: CustomerFilter; label: string }[] = [
    { value: 'All', label: $localize`All` },
    { value: 'Upcoming', label: $localize`With a ride booked` },
    { value: 'Returning', label: $localize`Returning` },
    { value: 'Direct', label: $localize`Booked on the website` },
    { value: 'ViaPartner', label: $localize`Via a partner` },
    { value: 'Marketing', label: $localize`Accept offers` },
  ]
  protected readonly sorts: { value: CustomerSort; label: string }[] = [
    { value: 'LastRide', label: $localize`Next and latest rides` },
    { value: 'Spent', label: $localize`Highest spend` },
    { value: 'Rides', label: $localize`Most rides` },
    { value: 'Name', label: $localize`Name` },
  ]
  protected readonly languages: { value: string; label: string }[] = [
    { value: 'it', label: $localize`Italian` },
    { value: 'en', label: $localize`English` },
    { value: 'de', label: $localize`German` },
    { value: 'fr', label: $localize`French` },
  ]

  protected readonly search = signal('')
  protected readonly filter = signal<CustomerFilter>('All')
  protected readonly sort = signal<CustomerSort>('LastRide')
  protected readonly page = signal(1)
  protected readonly list = signal<CustomerList | null>(null)
  protected readonly loading = signal(false)
  protected readonly exporting = signal(false)
  protected readonly error = signal<string | null>(null)
  private readonly typed = new Subject<string>()

  protected readonly pages = computed(() => {
    const l = this.list()
    return l ? Math.max(1, Math.ceil(l.total / l.pageSize)) : 1
  })

  // ── The card ───────────────────────────────────────────────────────────────
  protected readonly openId = signal<string | null>(null)
  protected readonly detail = signal<CustomerDetail | null>(null)
  protected readonly detailLoading = signal(false)
  protected readonly saving = signal(false)
  protected readonly formError = signal<string | null>(null)
  /** A ride opened from the history: the drawer shows the booking, with a way back. */
  protected readonly rideId = signal<string | null>(null)

  protected readonly form = this.fb.group({
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', Validators.maxLength(100)],
    email: ['', [Validators.email, Validators.maxLength(200)]],
    phone: [
      '',
      [Validators.required, Validators.pattern(/^\+?[0-9 ()-]{6,20}$/)],
    ],
    language: ['en'],
    marketingConsent: [false],
    notesForDriver: ['', Validators.maxLength(1000)],
  })

  ngOnInit(): void {
    const p = this.route.snapshot.queryParamMap
    this.search.set(p.get('search') ?? '')
    this.filter.set((p.get('filter') as CustomerFilter) ?? 'All')
    this.sort.set((p.get('sort') as CustomerSort) ?? 'LastRide')
    this.typed
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(value => {
        this.search.set(value)
        this.reload()
      })
    this.load()
    const open = p.get('open')
    if (open) this.open(open)
  }

  protected typedSearch(value: string): void {
    this.typed.next(value)
  }

  protected setFilter(filter: CustomerFilter): void {
    this.filter.set(filter)
    this.reload()
  }

  protected setSort(sort: CustomerSort): void {
    this.sort.set(sort)
    this.reload()
  }

  protected goTo(page: number): void {
    this.page.set(Math.min(Math.max(page, 1), this.pages()))
    this.load()
  }

  protected languageName(code: string): string {
    return this.languages.find(l => l.value === code)?.label ?? code
  }

  protected initials(name: string): string {
    return name
      .split(/\s+/)
      .filter(w => w.length > 0)
      .slice(0, 2)
      .map(w => w[0].toUpperCase())
      .join('')
  }

  // ── Card ───────────────────────────────────────────────────────────────────

  protected open(id: string): void {
    this.openId.set(id)
    this.rideId.set(null)
    this.detail.set(null)
    this.formError.set(null)
    this.detailLoading.set(true)
    this.api
      .get(id)
      .pipe(finalize(() => this.detailLoading.set(false)))
      .subscribe({
        next: d => this.show(d),
        error: err => this.formError.set(problemMessage(err)),
      })
  }

  protected close(): void {
    this.openId.set(null)
    this.rideId.set(null)
  }

  protected save(): void {
    const id = this.openId()
    if (!id || this.form.invalid) {
      this.form.markAllAsTouched()
      return
    }
    const v = this.form.getRawValue()
    this.saving.set(true)
    this.formError.set(null)
    this.api
      .update(id, {
        ...v,
        email: v.email.trim() || null,
        notesForDriver: v.notesForDriver.trim() || null,
      })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: d => {
          this.show(d)
          this.toast.success($localize`Customer saved.`)
          this.load()
        },
        error: err => this.formError.set(problemMessage(err)),
      })
  }

  /** The ride's booking changed (status, price…): the card and the list follow. */
  protected rideChanged(): void {
    const id = this.openId()
    if (id) {
      this.api.get(id).subscribe({ next: d => this.detail.set(d) })
    }
    this.load()
  }

  // ── Export ─────────────────────────────────────────────────────────────────

  protected exportExcel(): void {
    this.exporting.set(true)
    this.api
      .export(this.query())
      .pipe(finalize(() => this.exporting.set(false)))
      .subscribe({
        next: items => {
          const header = [
            $localize`Name`,
            $localize`Phone`,
            $localize`Email`,
            $localize`Language`,
            $localize`Partner`,
            $localize`Rides`,
            $localize`Cancelled`,
            $localize`Spent`,
            $localize`Last ride`,
            $localize`Next ride`,
            $localize`Accepts offers`,
          ]
          const yes = $localize`Yes`
          const rows = items.map(c => [
            c.name,
            c.phone,
            c.email ?? '',
            this.languageName(c.language),
            c.partnerName ?? '',
            c.rides,
            c.cancelled,
            c.spent,
            c.lastRide ?? '',
            c.nextRide ?? '',
            c.marketingConsent ? yes : '',
          ])
          downloadCsv(`customers-${this.today}.csv`, [header, ...rows])
        },
        error: err => this.error.set(problemMessage(err)),
      })
  }

  private show(d: CustomerDetail): void {
    this.detail.set(d)
    this.form.reset({
      firstName: d.firstName,
      lastName: d.lastName,
      email: d.email ?? '',
      phone: d.phone,
      language: d.language,
      marketingConsent: d.marketingConsent,
      notesForDriver: d.notesForDriver ?? '',
    })
  }

  private query(): CustomerQuery {
    return {
      search: this.search(),
      filter: this.filter(),
      sort: this.sort(),
      page: this.page(),
      pageSize: PAGE_SIZE,
    }
  }

  private reload(): void {
    this.page.set(1)
    this.load()
  }

  private load(): void {
    const q = this.query()
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        search: q.search.trim() || null,
        filter: q.filter === 'All' ? null : q.filter,
        sort: q.sort === 'LastRide' ? null : q.sort,
        open: null,
      },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    })
    this.loading.set(true)
    this.error.set(null)
    this.api
      .list(q)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: l => this.list.set(l),
        error: err => this.error.set(problemMessage(err)),
      })
  }
}
