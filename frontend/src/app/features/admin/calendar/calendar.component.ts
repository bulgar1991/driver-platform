import { DatePipe, DecimalPipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  afterNextRender,
  DestroyRef,
  ElementRef,
  inject,
  Injector,
  OnInit,
  signal,
  viewChild,
} from '@angular/core'
import { RouterLink } from '@angular/router'
import { MatCheckbox } from '@angular/material/checkbox'
import { MatFormField, MatLabel } from '@angular/material/form-field'
import { MatInput } from '@angular/material/input'
import { Drawer } from 'primeng/drawer'
import { finalize } from 'rxjs'
import {
  BookingListItem,
  CalendarData,
  CalendarRide,
  Shift,
  ShiftResult,
} from '@/core/models'
import { AdminCalendarService } from '@/core/services/admin-calendar.service'
import { AuthService } from '@/core/services/auth.service'
import { ToastService } from '@/core/services/toast.service'
import { downloadCsv } from '@/core/utils/csv'
import { nowIn, OFFICE_TIME_ZONE, todayInOffice } from '@/core/utils/local-time'
import { problemMessage } from '@/core/utils/problem'
import {
  BOOKING_STATUS_LABEL,
  SHIFT_STATUS_LABEL,
} from '@/core/utils/status-labels'
import {
  DialogComponent,
  DialogOpenDirective,
} from '@/shared/components/dialog'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { ADMIN_TEXT } from '@/features/admin/admin-texts'
import { BookingDetailComponent } from '@/features/admin/bookings/booking-detail/booking-detail.component'
import { ShiftEditorComponent } from '@/features/admin/shifts/shift-editor/shift-editor.component'
import {
  Bar,
  buildRows,
  GroupBy,
  Period,
  periodRange,
  Row,
  shiftAnchor,
  ticks,
  wallMinutes,
} from './calendar-layout'
import { MiniMonthComponent } from './mini-month/mini-month.component'

/** Height of one lane of bars, and the padding above and below them (px; mirrored in the SCSS). */
const LANE = 30
const PAD = 6
/** Width of a day on the time axis, per period: wide enough to read, then the timeline scrolls. */
const DAY_WIDTH: Record<Period, number> = {
  day: 1440,
  days2: 840,
  days3: 620,
  week: 220,
  weeks2: 130,
  month: 64,
}

/** Shortest bar on screen (px), per period. */
const MIN_BAR: Record<Period, number> = {
  day: 104,
  days2: 96,
  days3: 90,
  week: 64,
  weeks2: 40,
  month: 22,
}

type Tone =
  'pending' | 'draft' | 'sent' | 'confirmed' | 'onboard' | 'done' | 'cancelled'

interface Band {
  x: number
  y: number
  w: number
  h: number
}

/**
 * Admin → Calendar: every ride of the period on a timeline, one row per vehicle, driver, customer or class.
 * Click a ride to open it; right-click, Ctrl/⌘-click, a row title or a rectangle drawn with the mouse
 * selects rides, and the selection becomes a shift (the booking manager). Also as a table.
 */
@Component({
  selector: 'app-calendar',
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    MatCheckbox,
    MatFormField,
    MatLabel,
    MatInput,
    Drawer,
    DialogComponent,
    DialogOpenDirective,
    ButtonComponent,
    IconComponent,
    BookingDetailComponent,
    ShiftEditorComponent,
    MiniMonthComponent,
  ],
  templateUrl: './calendar.component.html',
  styleUrl: './calendar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'clearSelection()' },
})
export class CalendarComponent implements OnInit {
  protected readonly text = ADMIN_TEXT
  protected readonly laneHeight = LANE
  protected readonly lanePad = PAD
  protected readonly statusLabel = BOOKING_STATUS_LABEL
  protected readonly shiftLabel = SHIFT_STATUS_LABEL
  protected readonly isAdmin = inject(AuthService).hasRole('Admin')
  protected readonly tx = {
    notAssigned: $localize`To assign`,
    newShift: $localize`New shift`,
    editShift: $localize`Edit shift`,
    booking: $localize`Booking`,
  }
  protected readonly periods: { value: Period; label: string }[] = [
    { value: 'day', label: $localize`Day` },
    { value: 'days2', label: $localize`2 days` },
    { value: 'days3', label: $localize`3 days` },
    { value: 'week', label: $localize`Week` },
    { value: 'weeks2', label: $localize`2 weeks` },
    { value: 'month', label: $localize`Month` },
  ]
  protected readonly groups: {
    value: GroupBy
    label: string
    icon: 'car' | 'users' | 'briefcase' | 'tag'
  }[] = [
    { value: 'vehicles', label: $localize`Vehicles`, icon: 'car' },
    { value: 'drivers', label: $localize`Drivers`, icon: 'users' },
    { value: 'customers', label: $localize`Customers`, icon: 'briefcase' },
    { value: 'category', label: $localize`Class`, icon: 'tag' },
  ]
  protected readonly tones: { tone: Tone; label: string }[] = [
    { tone: 'pending', label: $localize`To assign` },
    { tone: 'draft', label: $localize`Draft shift` },
    { tone: 'sent', label: $localize`Sent to driver` },
    { tone: 'confirmed', label: $localize`Confirmed` },
    { tone: 'onboard', label: $localize`On board` },
    { tone: 'done', label: $localize`Completed` },
    { tone: 'cancelled', label: $localize`Cancelled` },
  ]

  private readonly api = inject(AdminCalendarService)
  private readonly toast = inject(ToastService)
  private readonly rowsElement = viewChild<ElementRef<HTMLElement>>('rowsBox')
  private readonly scrollElement =
    viewChild<ElementRef<HTMLElement>>('scrollBox')
  private readonly injector = inject(Injector)

  protected readonly today = todayInOffice()
  protected readonly anchor = signal(this.today)
  protected readonly period = signal<Period>('day')
  protected readonly groupBy = signal<GroupBy>('vehicles')
  protected readonly mode = signal<'timeline' | 'table'>('timeline')
  protected readonly search = signal('')
  protected readonly showCancelled = signal(false)
  protected readonly onlyPending = signal(false)

  protected readonly data = signal<CalendarData | null>(null)
  protected readonly loading = signal(true)
  protected readonly error = signal<string | null>(null)
  protected readonly now = signal(nowIn(OFFICE_TIME_ZONE))

  protected readonly groupLabel = computed(
    () => this.groups.find(g => g.value === this.groupBy())?.label ?? ''
  )
  protected readonly range = computed(() =>
    periodRange(this.anchor(), this.period())
  )
  protected readonly ticks = computed(() =>
    ticks(this.range().from, this.range().to)
  )
  protected readonly trackWidth = computed(
    () => this.ticks().filter(t => t.major).length * DAY_WIDTH[this.period()]
  )

  /** Rides after the search and the toggles. */
  protected readonly rides = computed(() => {
    const term = this.search().trim().toLowerCase()
    return (this.data()?.rides ?? []).filter(
      r =>
        (this.showCancelled() || r.status !== 'Cancelled') &&
        (!this.onlyPending() || this.tone(r) === 'pending') &&
        (!term ||
          [
            r.reference,
            r.clientName,
            r.partnerName,
            r.pickupLabel,
            r.dropoffLabel,
            r.flightNumber,
            r.vehiclePlate,
            r.driverName,
          ].some(v => v?.toLowerCase().includes(term)))
    )
  })

  protected readonly rows = computed<Row[]>(() => {
    const data = this.data()
    return data
      ? buildRows(
          data,
          this.rides(),
          this.groupBy(),
          {
            notAssigned: this.tx.notAssigned,
            categoryName: code => ADMIN_TEXT.categories[code],
          },
          (MIN_BAR[this.period()] / DAY_WIDTH[this.period()]) * 1440
        )
      : []
  })

  /** Top of each row inside the timeline body (px), for the rubber-band selection. */
  private readonly rowTops = computed(() => {
    let top = 0
    return this.rows().map(r => {
      const t = top
      top += this.rowHeight(r)
      return t
    })
  })

  protected readonly nowLeft = computed(() => {
    const start = wallMinutes(this.range().from)
    const span = wallMinutes(this.range().to) + 1440 - start
    const at = wallMinutes(this.now()) - start
    return at >= 0 && at <= span ? (at / span) * 100 : null
  })

  /** Period totals, without cancelled rides. */
  protected readonly totals = computed(() => {
    const live = this.rides().filter(r => r.status !== 'Cancelled')
    const revenue = sum(live.map(r => r.totalPrice))
    const pay = sum(live.map(r => r.driverPay ?? 0))
    return {
      services: live.length,
      pending: live.filter(r => this.tone(r) === 'pending').length,
      revenue,
      partner: sum(live.filter(r => r.partnerId).map(r => r.totalPrice)),
      website: sum(
        live.filter(r => r.source === 'Website').map(r => r.totalPrice)
      ),
      driverPay: pay,
      margin: revenue - pay,
      km: sum(live.map(r => r.distanceKm)),
    }
  })

  protected readonly tableRides = computed(() =>
    [...this.rides()].sort((a, b) => a.start.localeCompare(b.start))
  )

  // Selection = booking manager
  protected readonly selected = signal<ReadonlySet<string>>(new Set())
  protected readonly selection = computed(() =>
    (this.data()?.rides ?? []).filter(r => this.selected().has(r.id))
  )
  protected readonly selectionInfo = computed(() => {
    const list = this.selection()
    const shifts = new Set(list.map(r => r.assignmentId))
    const allPending =
      list.length > 0 && list.every(r => this.tone(r) === 'pending')
    const oneShift =
      list.length > 0 && shifts.size === 1 && list[0].assignmentId !== null
    return {
      count: list.length,
      revenue: sum(
        list.filter(r => r.status !== 'Cancelled').map(r => r.totalPrice)
      ),
      allPending,
      shiftId: oneShift ? list[0].assignmentId : null,
    }
  })
  protected readonly band = signal<Band | null>(null)
  private bandStart: { x: number; y: number; additive: boolean } | null = null

  // Drawers and dialogs
  protected readonly openId = signal<string | null>(null)
  protected readonly editorOpen = signal(false)
  protected readonly editingShift = signal<Shift | null>(null)
  protected readonly candidates = signal<BookingListItem[]>([])
  protected readonly preselected = signal<string[]>([])

  constructor() {
    const timer = setInterval(
      () => this.now.set(nowIn(OFFICE_TIME_ZONE)),
      60_000
    )
    inject(DestroyRef).onDestroy(() => clearInterval(timer))
  }

  ngOnInit(): void {
    this.load()
  }

  protected rowHeight(row: Row): number {
    return Math.max(row.lanes * LANE + PAD * 2, 46)
  }

  protected tone(r: CalendarRide): Tone {
    if (r.status === 'Cancelled') return 'cancelled'
    if (r.status === 'Completed') return 'done'
    if (r.status === 'PickedUp') return 'onboard'
    if (!r.assignmentId) return 'pending'
    switch (r.shiftStatus) {
      case 'Confirmed':
        return 'confirmed'
      case 'Sent':
      case 'Seen':
        return 'sent'
      default:
        return 'draft'
    }
  }

  protected barTitle(bar: Bar): string {
    const r = bar.ride
    return [
      `${r.reference} · ${r.pickupLocal.slice(11, 16)} · ${r.durationMinutes} min`,
      r.partnerName ? `${r.clientName} (${r.partnerName})` : r.clientName,
      `${r.pickupLabel} → ${r.dropoffLabel}`,
      r.driverName
        ? `${r.driverName} · ${r.vehiclePlate}`
        : this.tx.notAssigned,
    ].join('\n')
  }

  // Period

  protected setPeriod(period: Period): void {
    this.period.set(period)
    this.load()
  }

  protected step(direction: number): void {
    this.anchor.set(shiftAnchor(this.anchor(), this.period(), direction))
    this.load()
  }

  protected goTo(date: string): void {
    this.anchor.set(date)
    this.load()
  }

  // Selection

  protected barClick(event: MouseEvent, ride: CalendarRide): void {
    if (event.ctrlKey || event.metaKey || event.shiftKey) {
      this.toggle(ride.id)
    } else {
      this.openId.set(ride.id)
    }
  }

  protected barMenu(event: MouseEvent, ride: CalendarRide): void {
    event.preventDefault()
    this.toggle(ride.id)
  }

  protected toggle(id: string): void {
    const next = new Set(this.selected())
    if (next.has(id)) {
      next.delete(id)
    } else {
      next.add(id)
    }
    this.selected.set(next)
  }

  /** A row title selects every ride of the row (or unselects them when all were). */
  protected selectRow(row: Row): void {
    const ids = row.bars.map(b => b.ride.id)
    const all = ids.length > 0 && ids.every(id => this.selected().has(id))
    const next = new Set(this.selected())
    for (const id of ids) {
      if (all) {
        next.delete(id)
      } else {
        next.add(id)
      }
    }
    this.selected.set(next)
  }

  protected selectAllShown(): void {
    this.selected.set(new Set(this.rides().map(r => r.id)))
  }

  protected resetSelection(): void {
    this.selected.set(new Set())
  }

  clearSelection(): void {
    if (this.selected().size && !this.editorOpen() && !this.openId()) {
      this.selected.set(new Set())
    }
  }

  protected pointerDown(event: PointerEvent): void {
    const target = event.target as HTMLElement
    if (event.button !== 0 || target.closest('.cal__bar, .cal__row-head')) {
      return
    }
    const box = this.rowsElement()?.nativeElement.getBoundingClientRect()
    if (!box) return
    this.bandStart = {
      x: event.clientX - box.left,
      y: event.clientY - box.top,
      additive: event.shiftKey || event.ctrlKey || event.metaKey,
    }
    ;(event.currentTarget as HTMLElement).setPointerCapture(event.pointerId)
  }

  protected pointerMove(event: PointerEvent): void {
    if (!this.bandStart) return
    const box = this.rowsElement()!.nativeElement.getBoundingClientRect()
    const x = event.clientX - box.left
    const y = event.clientY - box.top
    const s = this.bandStart
    if (!this.band() && Math.abs(x - s.x) + Math.abs(y - s.y) < 6) {
      return
    }
    this.band.set({
      x: Math.min(x, s.x),
      y: Math.min(y, s.y),
      w: Math.abs(x - s.x),
      h: Math.abs(y - s.y),
    })
  }

  /** Selects the rides the rectangle touches: worked out from the layout, not from the page. */
  protected pointerUp(): void {
    const band = this.band()
    const start = this.bandStart
    this.bandStart = null
    this.band.set(null)
    if (!band || !start) return
    const element = this.rowsElement()!.nativeElement
    const headWidth =
      element.querySelector<HTMLElement>('.cal__row-head')?.offsetWidth ?? 0
    const track = this.trackWidth()
    const x1 = ((band.x - headWidth) / track) * 100
    const x2 = ((band.x + band.w - headWidth) / track) * 100
    const next = new Set(start.additive ? this.selected() : [])
    this.rows().forEach((row, i) => {
      const top = this.rowTops()[i]
      for (const bar of row.bars) {
        const y1 = top + PAD + bar.lane * LANE
        const y2 = y1 + LANE - 4
        if (
          bar.left < x2 &&
          bar.left + bar.width > x1 &&
          y1 < band.y + band.h &&
          y2 > band.y
        ) {
          next.add(bar.ride.id)
        }
      }
    })
    this.selected.set(next)
  }

  // Booking manager

  protected buildShift(): void {
    const list = this.selection().filter(r => this.tone(r) === 'pending')
    this.editingShift.set(null)
    this.candidates.set(list.map(toListItem))
    this.preselected.set(list.map(r => r.id))
    this.editorOpen.set(true)
  }

  protected openShift(id: string): void {
    this.api.shift(id).subscribe({
      next: shift => {
        this.editingShift.set(shift)
        this.candidates.set(
          (this.data()?.rides ?? [])
            .filter(r => this.tone(r) === 'pending')
            .map(toListItem)
        )
        this.preselected.set([])
        this.editorOpen.set(true)
      },
      error: err => this.toast.error(problemMessage(err)),
    })
  }

  protected shiftSaved(result: ShiftResult): void {
    this.editorOpen.set(false)
    this.selected.set(new Set())
    for (const warning of result.warnings) {
      this.toast.info(warning)
    }
    this.load()
  }

  /** CSV of the selection, or of everything shown when nothing is selected. */
  protected export(): void {
    const list = this.selection().length ? this.selection() : this.tableRides()
    const header = [
      $localize`Date`,
      $localize`Time`,
      $localize`Reference`,
      $localize`Customer`,
      $localize`Partner`,
      $localize`From`,
      $localize`To`,
      $localize`Passengers`,
      $localize`Class`,
      $localize`Driver`,
      $localize`Vehicle`,
      $localize`Status`,
      $localize`Price`,
      ...(this.isAdmin ? [$localize`Driver pay`] : []),
    ]
    const rows = list.map(r => [
      r.pickupLocal.slice(0, 10),
      r.pickupLocal.slice(11, 16),
      r.reference,
      r.clientName,
      r.partnerName ?? '',
      r.pickupLabel,
      r.dropoffLabel,
      r.passengers,
      ADMIN_TEXT.categories[r.vehicleCategory],
      r.driverName ?? '',
      r.vehiclePlate ?? '',
      BOOKING_STATUS_LABEL[r.status],
      r.totalPrice,
      ...(this.isAdmin ? [r.driverPay ?? ''] : []),
    ])
    const { from, to } = this.range()
    downloadCsv(`services-${from}-${to}.csv`, [header, ...rows])
  }

  /** Day views open an hour before the first ride (or now, today), not at midnight. */
  private scrollToWork(): void {
    const box = this.scrollElement()?.nativeElement
    const data = this.data()
    if (
      !box ||
      !data ||
      this.period() === 'month' ||
      this.period() === 'weeks2'
    ) {
      return
    }
    const start = wallMinutes(this.range().from)
    const span = wallMinutes(this.range().to) + 1440 - start
    const first = this.rides()[0]
    const focus =
      this.nowLeft() !== null && this.period() === 'day'
        ? wallMinutes(this.now()) - start
        : first
          ? wallMinutes(first.start) - start
          : 7 * 60
    box.scrollLeft = Math.max(0, ((focus - 60) / span) * this.trackWidth())
  }

  protected load(): void {
    const { from, to } = this.range()
    this.loading.set(true)
    this.error.set(null)
    this.api
      .calendar(from, to)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: data => {
          this.data.set(data)
          afterNextRender(() => this.scrollToWork(), {
            injector: this.injector,
          })
          // Keep only what still exists in the new period.
          const ids = new Set(data.rides.map(r => r.id))
          this.selected.set(
            new Set([...this.selected()].filter(id => ids.has(id)))
          )
        },
        error: err => this.error.set(problemMessage(err)),
      })
  }
}

function sum(values: number[]): number {
  return values.reduce((a, b) => a + b, 0)
}

/** What the shift editor needs to list a ride. */
function toListItem(r: CalendarRide): BookingListItem {
  return {
    id: r.id,
    reference: r.reference,
    status: r.status,
    source: r.source,
    pickupAt: r.pickupLocal,
    pickupLabel: r.pickupLabel,
    dropoffLabel: r.dropoffLabel,
    clientName: r.clientName,
    clientPhone: '',
    passengers: r.passengers,
    largeLuggage: r.largeLuggage,
    smallLuggage: r.smallLuggage,
    childSeats: r.childSeats,
    vehicleCategory: r.vehicleCategory,
    distanceKm: r.distanceKm,
    durationMinutes: r.durationMinutes,
    totalPrice: r.totalPrice,
    flightNumber: r.flightNumber,
    assignmentId: r.assignmentId,
    driverName: r.driverName,
    vehiclePlate: r.vehiclePlate,
    isReturnLeg: false,
    partnerName: r.partnerName,
  }
}
