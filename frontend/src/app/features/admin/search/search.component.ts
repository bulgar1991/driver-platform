import { DatePipe, DecimalPipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core'
import { FormBuilder, ReactiveFormsModule } from '@angular/forms'
import { ActivatedRoute, Router } from '@angular/router'
import { MatCheckbox } from '@angular/material/checkbox'
import { MatFormField, MatLabel } from '@angular/material/form-field'
import { MatInput } from '@angular/material/input'
import { MatOption, MatSelect } from '@angular/material/select'
import { Drawer } from 'primeng/drawer'
import { finalize } from 'rxjs'
import {
  BookingSource,
  BookingStatus,
  PaymentFilter,
  PaymentStatus,
  ReportKind,
  SearchOptions,
  SearchQuery,
  SearchResult,
  VehicleCategory,
} from '@/core/models'
import { AdminSearchService } from '@/core/services/admin-search.service'
import { AuthService } from '@/core/services/auth.service'
import { VehicleClassService } from '@/core/services/vehicle-class.service'
import { downloadCsv } from '@/core/utils/csv'
import { todayInOffice } from '@/core/utils/local-time'
import { problemMessage } from '@/core/utils/problem'
import {
  BOOKING_STATUS_LABEL,
  BOOKING_STATUS_TONE,
} from '@/core/utils/status-labels'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { IconName } from '@/core/models'
import { ADMIN_TEXT } from '@/features/admin/admin-texts'
import { BookingDetailComponent } from '@/features/admin/bookings/booking-detail/booking-detail.component'
import { addDays, mondayOf } from '@/features/admin/calendar/calendar-layout'

interface Preset {
  key: string
  label: string
  range: () => { from: string; to: string }
}

/**
 * Admin → Search: rides by date, text, status, source, partner, driver, vehicle, class and payment, with
 * totals, an Excel export and the PDF reports the filters allow (a driver report needs a driver…).
 * The filters live in the address, so a search can be bookmarked or sent to a colleague.
 */
@Component({
  selector: 'app-search',
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    MatCheckbox,
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
  templateUrl: './search.component.html',
  styleUrl: './search.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SearchComponent implements OnInit {
  protected readonly text = ADMIN_TEXT
  protected readonly statusLabel = BOOKING_STATUS_LABEL
  protected readonly statusTone = BOOKING_STATUS_TONE
  protected readonly isAdmin = inject(AuthService).hasRole('Admin')
  private readonly api = inject(AdminSearchService)
  private readonly router = inject(Router)
  private readonly route = inject(ActivatedRoute)
  private readonly fb = inject(FormBuilder).nonNullable
  protected readonly classes = inject(VehicleClassService).classes

  protected readonly today = todayInOffice()
  protected readonly presets: Preset[] = [
    {
      key: 'today',
      label: $localize`Today`,
      range: () => ({ from: this.today, to: this.today }),
    },
    {
      key: 'tomorrow',
      label: $localize`Tomorrow`,
      range: () => ({
        from: addDays(this.today, 1),
        to: addDays(this.today, 1),
      }),
    },
    {
      key: 'week',
      label: $localize`This week`,
      range: () => ({
        from: mondayOf(this.today),
        to: addDays(mondayOf(this.today), 6),
      }),
    },
    {
      key: 'month',
      label: $localize`This month`,
      range: () => monthRange(this.today, 0),
    },
    {
      key: 'lastMonth',
      label: $localize`Last month`,
      range: () => monthRange(this.today, -1),
    },
    {
      key: 'year',
      label: $localize`This year`,
      range: () => ({
        from: this.today.slice(0, 4) + '-01-01',
        to: this.today.slice(0, 4) + '-12-31',
      }),
    },
  ]
  protected readonly statuses: BookingStatus[] = [
    'New',
    'Assigned',
    'PickedUp',
    'Completed',
    'Cancelled',
  ]
  protected readonly sources: { value: BookingSource; label: string }[] = [
    { value: 'Website', label: $localize`Website` },
    { value: 'Partner', label: $localize`Partner API / panel` },
    { value: 'Booking', label: $localize`Booking.com` },
    { value: 'Office', label: $localize`Office (phone, email)` },
  ]
  protected readonly payments: { value: PaymentFilter; label: string }[] = [
    { value: 'Any', label: $localize`Any` },
    { value: 'Paid', label: $localize`Paid online` },
    { value: 'Refunded', label: $localize`Refunded` },
    { value: 'NotPaidOnline', label: $localize`Not paid online (invoiced)` },
  ]
  protected readonly paymentLabel: Partial<Record<PaymentStatus, string>> = {
    Succeeded: $localize`Paid`,
    Pending: $localize`Pending`,
    Failed: $localize`Failed`,
    Refunded: $localize`Refunded`,
    PartiallyRefunded: $localize`Part refunded`,
  }
  protected readonly invoiced = $localize`Invoiced`
  protected readonly reportInfo: Record<
    ReportKind,
    { label: string; hint: string; icon: IconName }
  > = {
    List: {
      label: $localize`List of services`,
      hint: $localize`Every ride found`,
      icon: 'grid',
    },
    DailySheet: {
      label: $localize`Operations sheet`,
      hint: $localize`The day's rides per driver`,
      icon: 'route',
    },
    Driver: {
      label: $localize`Driver report`,
      hint: $localize`Rides and km of the driver`,
      icon: 'users',
    },
    Partner: {
      label: $localize`Partner statement`,
      hint: $localize`Rides and amounts to invoice`,
      icon: 'briefcase',
    },
    Vehicle: {
      label: $localize`Vehicle report`,
      hint: $localize`Rides and km of the vehicle`,
      icon: 'car',
    },
  }

  protected readonly options = signal<SearchOptions | null>(null)
  protected readonly result = signal<SearchResult | null>(null)
  protected readonly loading = signal(false)
  protected readonly error = signal<string | null>(null)
  protected readonly printing = signal<ReportKind | null>(null)
  protected readonly openId = signal<string | null>(null)
  protected readonly activePreset = signal<string | null>('today')
  /** The query of the results shown (reports use it, not the form being edited). */
  private readonly lastQuery = signal<SearchQuery | null>(null)

  protected readonly form = this.fb.group({
    from: [this.today],
    to: [this.today],
    text: [''],
    statuses: this.fb.control<BookingStatus[]>([]),
    source: this.fb.control<BookingSource | null>(null),
    partnerId: this.fb.control<string | null>(null),
    driverId: this.fb.control<string | null>(null),
    vehicleId: this.fb.control<string | null>(null),
    category: this.fb.control<VehicleCategory | null>(null),
    payment: this.fb.control<PaymentFilter>('Any'),
    unassigned: [false],
    includeCancelled: [false],
  })

  /** Which reports the results allow, in a fixed order. */
  protected readonly reports = computed(() => this.result()?.reports ?? [])
  /** A report about one driver, partner or vehicle is already offered. */
  protected readonly focused = computed(() =>
    this.reports().some(
      k => k === 'Driver' || k === 'Partner' || k === 'Vehicle'
    )
  )
  ngOnInit(): void {
    this.api.options().subscribe({ next: o => this.options.set(o) })
    const p = this.route.snapshot.queryParamMap
    if (p.has('from')) {
      this.form.patchValue({
        from: p.get('from') ?? this.today,
        to: p.get('to') ?? this.today,
        text: p.get('text') ?? '',
        statuses: p.getAll('statuses') as BookingStatus[],
        source: (p.get('source') as BookingSource) ?? null,
        partnerId: p.get('partnerId'),
        driverId: p.get('driverId'),
        vehicleId: p.get('vehicleId'),
        category: p.get('category'),
        payment: (p.get('payment') as PaymentFilter) ?? 'Any',
        unassigned: p.get('unassigned') === 'true',
        includeCancelled: p.get('includeCancelled') === 'true',
      })
      this.activePreset.set(
        this.presetOf(this.form.getRawValue().from, this.form.getRawValue().to)
      )
    }
    this.search()
  }

  protected toggleStatus(status: BookingStatus): void {
    const current = this.form.controls.statuses.value
    this.form.controls.statuses.setValue(
      current.includes(status)
        ? current.filter(s => s !== status)
        : [...current, status]
    )
  }

  protected usePreset(preset: Preset): void {
    this.form.patchValue(preset.range())
    this.activePreset.set(preset.key)
    this.search()
  }

  protected datesChanged(): void {
    const v = this.form.getRawValue()
    this.activePreset.set(this.presetOf(v.from, v.to))
  }

  protected reset(): void {
    this.form.reset({
      from: this.today,
      to: this.today,
      payment: 'Any',
      statuses: [],
    })
    this.activePreset.set('today')
    this.search()
  }

  protected search(): void {
    const v = this.form.getRawValue()
    if (!v.from || !v.to) {
      return
    }
    const query: SearchQuery = {
      ...v,
      from: v.from <= v.to ? v.from : v.to,
      to: v.from <= v.to ? v.to : v.from,
      text: v.text.trim(),
    }
    this.loading.set(true)
    this.error.set(null)
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: urlParams(query),
      replaceUrl: true,
    })
    this.api
      .search(query)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: result => {
          this.result.set(result)
          this.lastQuery.set(query)
        },
        error: err => this.error.set(problemMessage(err)),
      })
  }

  protected print(kind: ReportKind): void {
    const query = this.lastQuery()
    if (!query) return
    this.printing.set(kind)
    this.api
      .report(query, kind)
      .pipe(finalize(() => this.printing.set(null)))
      .subscribe({
        next: blob => {
          const url = URL.createObjectURL(blob)
          // Opens the PDF in a new tab to read or print; the browser can save it from there.
          const tab = window.open(url, '_blank')
          if (!tab) {
            const link = document.createElement('a')
            link.href = url
            link.download = `${kind.toLowerCase()}-${query.from}-${query.to}.pdf`
            link.click()
          }
          setTimeout(() => URL.revokeObjectURL(url), 60_000)
        },
        error: err => this.error.set(problemMessage(err)),
      })
  }

  /** CSV for Excel: every ride found, with the columns that matter for these filters. */
  protected exportExcel(): void {
    const result = this.result()
    const query = this.lastQuery()
    if (!result || !query) return
    const header = [
      $localize`Date`,
      $localize`Time`,
      $localize`Reference`,
      $localize`Customer`,
      $localize`Phone`,
      $localize`Email`,
      $localize`Partner`,
      $localize`Partner reference`,
      $localize`From`,
      $localize`To`,
      $localize`Passengers`,
      $localize`Bags`,
      $localize`Class`,
      $localize`Km`,
      $localize`Flight`,
      $localize`Driver`,
      $localize`Vehicle`,
      $localize`Status`,
      $localize`Payment`,
      $localize`Price`,
      ...(this.isAdmin ? [$localize`Driver pay`] : []),
    ]
    const rows = result.rides.map(r => [
      r.pickupLocal.slice(0, 10),
      r.pickupLocal.slice(11, 16),
      r.reference,
      r.clientName,
      r.clientPhone,
      r.clientEmail ?? '',
      r.partnerName ?? '',
      r.externalReference ?? '',
      r.pickupLabel,
      r.dropoffLabel,
      r.passengers,
      r.largeLuggage + r.smallLuggage,
      ADMIN_TEXT.categories[r.vehicleCategory],
      r.distanceKm,
      r.flightNumber ?? '',
      r.driverName ?? '',
      r.vehiclePlate ?? '',
      BOOKING_STATUS_LABEL[r.status],
      r.paymentStatus
        ? (this.paymentLabel[r.paymentStatus] ?? r.paymentStatus)
        : this.invoiced,
      r.totalPrice,
      ...(this.isAdmin ? [r.driverPay ?? ''] : []),
    ])
    downloadCsv(`search-${query.from}-${query.to}.csv`, [header, ...rows])
  }

  private presetOf(from: string, to: string): string | null {
    return (
      this.presets.find(p => {
        const r = p.range()
        return r.from === from && r.to === to
      })?.key ?? null
    )
  }
}

function monthRange(
  today: string,
  offset: number
): { from: string; to: string } {
  const [y, m] = today.split('-').map(Number)
  const first = new Date(Date.UTC(y, m - 1 + offset, 1))
  const last = new Date(Date.UTC(y, m + offset, 0))
  return {
    from: first.toISOString().slice(0, 10),
    to: last.toISOString().slice(0, 10),
  }
}

/** The query as address parameters, empty filters left out. */
function urlParams(
  query: SearchQuery
): Record<string, string | string[] | null> {
  const params: Record<string, string | string[] | null> = {}
  for (const [key, value] of Object.entries(query)) {
    if (Array.isArray(value)) {
      params[key] = value.length ? value : null
    } else {
      params[key] =
        value === '' || value === false || value === null || value === 'Any'
          ? null
          : String(value)
    }
  }
  return params
}
