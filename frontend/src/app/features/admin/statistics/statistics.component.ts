import { DatePipe, DecimalPipe, PercentPipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core'
import { FormsModule } from '@angular/forms'
import { ActivatedRoute, Router, RouterLink } from '@angular/router'
import { MatFormField, MatLabel } from '@angular/material/form-field'
import { MatInput } from '@angular/material/input'
import { finalize } from 'rxjs'
import {
  StatisticsPoint,
  StatisticsResult,
  StatisticsShare,
  VehicleCategory,
} from '@/core/models'
import { AdminStatisticsService } from '@/core/services/admin-statistics.service'
import { CUSTOM_CLASS_NAMES } from '@/core/services/vehicle-class.service'
import { downloadCsv } from '@/core/utils/csv'
import { todayInOffice } from '@/core/utils/local-time'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { addDays, mondayOf } from '@/features/admin/calendar/calendar-layout'

type Metric = 'revenue' | 'rides'

interface Preset {
  key: string
  label: string
  range: () => { from: string; to: string }
}

/** A change against the previous period; null when there is nothing to compare with. */
export interface Delta {
  /** -1…∞, e.g. 0.12 = +12 %. */
  change: number
  up: boolean
}

export function delta(now: number, before: number): Delta | null {
  if (before === 0) return null
  const change = (now - before) / before
  return { change, up: change >= 0 }
}

/** 0 for an empty cell, then 1–5 by share of the busiest one. */
export function heatLevel(value: number, max: number): number {
  return value === 0 || max === 0
    ? 0
    : Math.max(1, Math.ceil((value / max) * 5))
}

function monthRange(today: string, offset: number) {
  const [y, m] = today.split('-').map(Number)
  return {
    from: new Date(Date.UTC(y, m - 1 + offset, 1)).toISOString().slice(0, 10),
    to: new Date(Date.UTC(y, m + offset, 0)).toISOString().slice(0, 10),
  }
}

/**
 * Admin → Statistics: rides and revenue of a period against the period just before it, where they
 * came from, which classes, drivers and cars did them, the busy hours and how far ahead clients book.
 */
@Component({
  selector: 'app-statistics',
  imports: [
    DatePipe,
    DecimalPipe,
    PercentPipe,
    FormsModule,
    RouterLink,
    MatFormField,
    MatLabel,
    MatInput,
    ButtonComponent,
    IconComponent,
  ],
  templateUrl: './statistics.component.html',
  styleUrl: './statistics.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatisticsComponent implements OnInit {
  private readonly api = inject(AdminStatisticsService)
  private readonly router = inject(Router)
  private readonly route = inject(ActivatedRoute)

  protected readonly today = todayInOffice()
  protected readonly presets: Preset[] = [
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
      key: '30',
      label: $localize`Last 30 days`,
      range: () => ({ from: addDays(this.today, -29), to: this.today }),
    },
    {
      key: '90',
      label: $localize`Last 90 days`,
      range: () => ({ from: addDays(this.today, -89), to: this.today }),
    },
    {
      key: 'year',
      label: $localize`This year`,
      range: () => ({
        from: this.today.slice(0, 4) + '-01-01',
        to: this.today.slice(0, 4) + '-12-31',
      }),
    },
    {
      key: '12m',
      label: $localize`Last 12 months`,
      range: () => ({ from: addDays(this.today, -364), to: this.today }),
    },
  ]

  protected readonly from = signal('')
  protected readonly to = signal('')
  protected readonly metric = signal<Metric>('revenue')
  protected readonly data = signal<StatisticsResult | null>(null)
  protected readonly loading = signal(false)
  protected readonly error = signal<string | null>(null)
  /** Bar under the pointer in the timeline. */
  protected readonly hover = signal<number | null>(null)
  protected readonly heatHover = signal<{ day: number; hour: number } | null>(
    null
  )

  protected readonly activePreset = computed(() => {
    const from = this.from()
    const to = this.to()
    return (
      this.presets.find(p => {
        const r = p.range()
        return r.from === from && r.to === to
      })?.key ?? null
    )
  })

  protected readonly weekdays = [
    $localize`Mon`,
    $localize`Tue`,
    $localize`Wed`,
    $localize`Thu`,
    $localize`Fri`,
    $localize`Sat`,
    $localize`Sun`,
  ]
  protected readonly hours = Array.from({ length: 24 }, (_, h) => h)
  protected readonly leadLabels = [
    $localize`Same day`,
    $localize`1–3 days`,
    $localize`4–7 days`,
    $localize`8–30 days`,
    $localize`Over a month`,
  ]

  protected readonly kpis = computed(() => {
    const d = this.data()
    if (!d) return []
    const t = d.totals
    const p = d.previous
    const items: {
      label: string
      value: number
      previous: number
      format: 'money' | 'number' | 'percent'
      /** For cancellations a rise is bad news. */
      lowerIsBetter?: boolean
      gold?: boolean
      hint?: string
    }[] = [
      {
        label: $localize`Revenue`,
        value: t.revenue,
        previous: p.revenue,
        format: 'money',
        gold: true,
      },
      {
        label: $localize`Rides`,
        value: t.rides,
        previous: p.rides,
        format: 'number',
      },
      {
        label: $localize`Average ride`,
        value: t.averageRide,
        previous: p.averageRide,
        format: 'money',
      },
      {
        label: $localize`Kilometres`,
        value: t.km,
        previous: p.km,
        format: 'number',
      },
      {
        label: $localize`Customers`,
        value: t.customers,
        previous: p.customers,
        format: 'number',
        hint: $localize`${t.newCustomers}:new: new`,
      },
      {
        label: $localize`Passengers`,
        value: t.passengers,
        previous: p.passengers,
        format: 'number',
      },
      {
        label: $localize`Cancelled`,
        value: t.cancelledRate,
        previous: p.cancelledRate,
        format: 'percent',
        lowerIsBetter: true,
        hint: $localize`${t.cancelled}:count: rides`,
      },
    ]
    if (t.driverPay !== null) {
      items.push({
        label: $localize`Driver pay`,
        value: t.driverPay,
        previous: p.driverPay ?? 0,
        format: 'money',
        lowerIsBetter: true,
      })
    }
    return items.map(k => ({ ...k, delta: delta(k.value, k.previous) }))
  })

  protected readonly bars = computed(() => {
    const d = this.data()
    if (!d) return []
    const metric = this.metric()
    const now = (p: StatisticsPoint) =>
      metric === 'revenue' ? p.revenue : p.rides
    const before = (p: StatisticsPoint) =>
      metric === 'revenue' ? p.previousRevenue : p.previousRides
    const max = Math.max(1, ...d.series.map(now), ...d.series.map(before))
    return d.series.map(p => ({
      point: p,
      value: now(p),
      previous: before(p),
      height: (now(p) / max) * 100,
      previousHeight: (before(p) / max) * 100,
    }))
  })

  /** Labels under the timeline: at most about 12, so they never collide. */
  protected readonly labelEvery = computed(() =>
    Math.max(1, Math.ceil(this.bars().length / 12))
  )

  protected readonly heatMax = computed(() =>
    Math.max(0, ...(this.data()?.heatmap.flat() ?? [0]))
  )

  protected readonly leadMax = computed(() =>
    Math.max(1, ...(this.data()?.leadTime ?? [1]))
  )

  ngOnInit(): void {
    const p = this.route.snapshot.queryParamMap
    const fallback = this.presets[1].range()
    this.from.set(p.get('from') ?? fallback.from)
    this.to.set(p.get('to') ?? fallback.to)
    this.load()
  }

  protected usePreset(preset: Preset): void {
    const r = preset.range()
    this.from.set(r.from)
    this.to.set(r.to)
    this.load()
  }

  protected datesChanged(): void {
    if (this.from() && this.to()) this.load()
  }

  protected heatLevel(value: number): number {
    return heatLevel(value, this.heatMax())
  }

  protected share(item: StatisticsShare, list: StatisticsShare[]): number {
    const max = Math.max(1, ...list.map(s => s.revenue))
    return (item.revenue / max) * 100
  }

  protected channelLabel(s: StatisticsShare): string {
    switch (s.key) {
      case 'Website':
        return $localize`Website`
      case 'Booking':
        return 'Booking.com'
      case 'Partner':
        return $localize`Partner`
      default:
        return s.label
    }
  }

  protected categoryLabel(s: StatisticsShare): string {
    return CUSTOM_CLASS_NAMES[s.key as VehicleCategory] ?? s.key
  }

  /** Search for the rides behind a row of the drivers or vehicles table. */
  protected searchParams(
    kind: 'driverId' | 'vehicleId' | 'partnerId',
    id: string
  ): Record<string, string> {
    return { from: this.from(), to: this.to(), [kind]: id }
  }

  protected exportExcel(): void {
    const d = this.data()
    if (!d) return
    const rows: (string | number)[][] = [
      [
        $localize`Period`,
        $localize`Rides`,
        $localize`Revenue`,
        $localize`Previous rides`,
        $localize`Previous revenue`,
      ],
      ...d.series.map(p => [
        p.start,
        p.rides,
        p.revenue,
        p.previousRides,
        p.previousRevenue,
      ]),
      [],
      [
        $localize`Driver`,
        $localize`Rides`,
        $localize`Kilometres`,
        $localize`Revenue`,
        ...(d.totals.driverPay !== null ? [$localize`Driver pay`] : []),
      ],
      ...d.drivers.map(s => [
        s.label,
        s.rides,
        s.km,
        s.revenue,
        ...(s.pay !== null ? [s.pay] : []),
      ]),
      [],
      [
        $localize`Vehicle`,
        $localize`Rides`,
        $localize`Kilometres`,
        $localize`Revenue`,
      ],
      ...d.vehicles.map(s => [s.label, s.rides, s.km, s.revenue]),
      [],
      [$localize`Channel`, $localize`Rides`, $localize`Revenue`],
      ...d.channels.map(s => [this.channelLabel(s), s.rides, s.revenue]),
    ]
    downloadCsv(`statistics-${d.from}-${d.to}.csv`, rows)
  }

  private load(): void {
    const from = this.from()
    const to = this.to()
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { from, to },
      replaceUrl: true,
    })
    this.loading.set(true)
    this.error.set(null)
    this.hover.set(null)
    this.api
      .get(from, to)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: d => this.data.set(d),
        error: err => this.error.set(problemMessage(err)),
      })
  }
}
