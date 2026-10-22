import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core'
import { takeUntilDestroyed } from '@angular/core/rxjs-interop'
import { FormBuilder, ReactiveFormsModule } from '@angular/forms'
import { Router } from '@angular/router'
import { Drawer } from 'primeng/drawer'
import { debounceTime, Subscription } from 'rxjs'
import { BookingListItem, BookingQuery, BookingStatus } from '@/core/models'
import { AdminDispatchService } from '@/core/services/admin-dispatch.service'
import { todayInItaly } from '@/core/utils/italy-time'
import { poll } from '@/core/utils/poll'
import { problemMessage } from '@/core/utils/problem'
import { BOOKING_STATUS_LABEL } from '@/core/utils/status-labels'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { StatusBadgeComponent } from '@/shared/components/status-badge/status-badge.component'
import { MoneyPipe } from '@/shared/pipes/money.pipe'
import { BookingDetailComponent } from './booking-detail/booking-detail.component'
import { ADMIN_TEXT } from '@/features/admin/admin-texts'

const PAGE_SIZE = 50

/** All bookings for the dispatcher, refreshed every 30 s. Bookings to assign can be picked into a new shift. */
@Component({
  selector: 'app-bookings',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    Drawer,
    BookingDetailComponent,
    ButtonComponent,
    IconComponent,
    StatusBadgeComponent,
    MoneyPipe,
  ],
  templateUrl: './bookings.component.html',
  styleUrl: './bookings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BookingsComponent {
  protected readonly text = ADMIN_TEXT
  protected readonly tx = {
    select: (reference: string) => $localize`Select ${reference}:reference:`,
  }

  /** Query params from the dashboard links. */
  readonly unassigned = input<string | undefined>(undefined)
  readonly status = input<BookingStatus | undefined>(undefined)

  private readonly api = inject(AdminDispatchService)
  private readonly router = inject(Router)
  private readonly destroyRef = inject(DestroyRef)
  private refresh?: Subscription

  protected readonly statuses = Object.entries(BOOKING_STATUS_LABEL) as [
    BookingStatus,
    string,
  ][]
  protected readonly bookings = signal<BookingListItem[]>([])
  protected readonly total = signal(0)
  protected readonly page = signal(1)
  protected readonly loading = signal(true)
  protected readonly error = signal<string | null>(null)
  protected readonly selected = signal<ReadonlySet<string>>(new Set())
  protected readonly openId = signal<string | null>(null)

  protected readonly pages = computed(() =>
    Math.max(1, Math.ceil(this.total() / PAGE_SIZE))
  )
  protected readonly selectedBookings = computed(() =>
    this.bookings().filter(b => this.selected().has(b.id))
  )

  protected readonly filters = inject(FormBuilder).nonNullable.group({
    from: [todayInItaly()],
    to: [todayInItaly(7)],
    status: ['' as BookingStatus | ''],
    unassigned: [false],
    includeUnpaid: [false],
    search: [''],
  })

  constructor() {
    effect(() => {
      const unassigned = this.unassigned()
      const status = this.status()
      untracked(() => {
        if (unassigned) {
          this.filters.patchValue(
            { unassigned: true, from: todayInItaly(), to: todayInItaly(2) },
            { emitEvent: false }
          )
        }
        if (status) {
          this.filters.patchValue(
            { status, includeUnpaid: status === 'AwaitingPayment' },
            { emitEvent: false }
          )
        }
        this.reload()
      })
    })
    this.filters.valueChanges
      .pipe(debounceTime(300), takeUntilDestroyed())
      .subscribe(() => {
        this.page.set(1)
        this.reload()
      })
  }

  protected canSelect(b: BookingListItem): boolean {
    return b.status === 'New' && !b.assignmentId
  }

  protected toggle(b: BookingListItem): void {
    const next = new Set(this.selected())
    if (next.has(b.id)) {
      next.delete(b.id)
    } else {
      next.add(b.id)
    }
    this.selected.set(next)
  }

  /** Opens the shift planner on that day with the chosen bookings ticked. */
  protected planShift(): void {
    const chosen = this.selectedBookings()
    if (!chosen.length) {
      return
    }
    void this.router.navigate(['/admin/shifts'], {
      queryParams: {
        date: chosen[0].pickupAt.slice(0, 10),
        bookings: chosen.map(b => b.id).join(','),
      },
    })
  }

  protected goToPage(page: number): void {
    this.page.set(page)
    this.reload()
  }

  protected onChanged(): void {
    this.reload()
  }

  private reload(): void {
    this.refresh?.unsubscribe()
    const v = this.filters.getRawValue()
    const query: BookingQuery = {
      ...v,
      status: v.status || null,
      page: this.page(),
      pageSize: PAGE_SIZE,
    }
    this.loading.set(true)
    this.refresh = poll(
      () => this.api.bookings(query),
      undefined,
      err => {
        this.loading.set(false)
        this.error.set(problemMessage(err))
      }
    )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(result => {
        this.loading.set(false)
        this.error.set(null)
        this.bookings.set(result.items)
        this.total.set(result.total)
        // Keep only selections that can still be assigned.
        const assignable = new Set(
          result.items.filter(b => this.canSelect(b)).map(b => b.id)
        )
        this.selected.set(
          new Set([...this.selected()].filter(id => assignable.has(id)))
        )
      })
  }
}
