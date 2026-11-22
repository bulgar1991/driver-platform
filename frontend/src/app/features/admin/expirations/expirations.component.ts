import { DatePipe, DecimalPipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { RouterLink } from '@angular/router'
import { MatCheckbox } from '@angular/material/checkbox'
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field'
import { MatInput } from '@angular/material/input'
import { finalize } from 'rxjs'
import {
  DeadlineOwner,
  DeadlineSource,
  ExpirationItem,
  ExpirationsResponse,
} from '@/core/models'
import { AdminExpirationsService } from '@/core/services/admin-expirations.service'
import { ToastService } from '@/core/services/toast.service'
import { downloadCsv } from '@/core/utils/csv'
import { todayInOffice } from '@/core/utils/local-time'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import {
  DialogComponent,
  DialogOpenDirective,
} from '@/shared/components/dialog'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { ADMIN_TEXT } from '@/features/admin/admin-texts'
import { DOCUMENT_LABEL } from '@/features/admin/staff-labels'

/** Which deadlines the cards at the top narrow the list to. */
type Focus = 'all' | 'expired' | 'week' | 'soon'
type Who = 'all' | DeadlineOwner

interface Group {
  key: string
  /** "Overdue", a month ("2026-12-01", formatted in the template) or "By odometer". */
  kind: 'overdue' | 'month' | 'odometer'
  month: string | null
  items: ExpirationItem[]
}

interface Step {
  label: string
  months?: number
  km?: number
}

/**
 * Admin → Expirations: every deadline of the drivers and the cars in one list (licences, NCC,
 * insurance, inspection, service and the cars' own deadlines by date or odometer), most urgent
 * first and grouped by month, with the dates still missing on the side. A deadline is renewed
 * here directly, without opening the driver's or car's card.
 */
@Component({
  selector: 'app-expirations',
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
    ButtonComponent,
    DialogComponent,
    DialogOpenDirective,
    IconComponent,
  ],
  templateUrl: './expirations.component.html',
  styleUrl: './expirations.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExpirationsComponent implements OnInit {
  protected readonly text = ADMIN_TEXT
  private readonly api = inject(AdminExpirationsService)
  private readonly toast = inject(ToastService)
  private readonly fb = inject(FormBuilder)

  protected readonly renewLabel = $localize`Renew`
  protected readonly today = todayInOffice()
  protected readonly data = signal<ExpirationsResponse | null>(null)
  protected readonly loading = signal(false)
  protected readonly error = signal<string | null>(null)
  protected readonly showInactive = signal(false)
  protected readonly focus = signal<Focus>('all')
  protected readonly who = signal<Who>('all')
  /** Days ahead shown; null = everything. Overdue deadlines are always shown. */
  protected readonly horizon = signal<number | null>(90)
  protected readonly query = signal('')

  protected readonly horizons: { days: number | null; label: string }[] = [
    { days: 30, label: $localize`30 days` },
    { days: 90, label: $localize`3 months` },
    { days: 180, label: $localize`6 months` },
    { days: 365, label: $localize`1 year` },
    { days: null, label: $localize`All` },
  ]
  protected readonly whoOptions: { value: Who; label: string }[] = [
    { value: 'all', label: $localize`Everyone` },
    { value: 'Driver', label: $localize`Drivers` },
    { value: 'Vehicle', label: $localize`Vehicles` },
  ]
  private readonly sourceLabel: Record<
    Exclude<DeadlineSource, 'DriverDocument' | 'VehicleDeadline'>,
    string
  > = {
    Insurance: $localize`Insurance`,
    Inspection: $localize`Inspection`,
    Service: $localize`Service`,
  }
  private readonly stateLabel = {
    Expired: $localize`Expired`,
    DueSoon: $localize`Due soon`,
    Later: $localize`Later`,
  }

  /** The deadline being renewed. */
  protected readonly renewing = signal<ExpirationItem | null>(null)
  protected readonly saving = signal(false)
  protected readonly formError = signal<string | null>(null)
  protected readonly form = this.fb.group({
    dueOn: this.fb.control<string>(''),
    dueKm: this.fb.control<number | null>(null, [
      Validators.min(0),
      Validators.max(2_000_000),
    ]),
  })

  /** Who and the text box apply to everything on the page, the cards' counts too. */
  private readonly scoped = computed(() => {
    const d = this.data()
    if (!d) return []
    const who = this.who()
    const q = this.query().trim().toLowerCase()
    return d.items.filter(
      i =>
        (who === 'all' || i.ownerKind === who) &&
        (!q || this.searchText(i).includes(q))
    )
  })

  protected readonly missing = computed(() => {
    const d = this.data()
    if (!d) return []
    const who = this.who()
    const q = this.query().trim().toLowerCase()
    return d.missing.filter(
      m =>
        (who === 'all' || m.ownerKind === who) &&
        (!q ||
          m.ownerName.toLowerCase().includes(q) ||
          m.problem.toLowerCase().includes(q))
    )
  })

  protected readonly counts = computed(() => {
    const items = this.scoped()
    return {
      expired: items.filter(i => i.state === 'Expired').length,
      week: items.filter(i => this.inWeek(i)).length,
      soon: items.filter(i => i.state === 'DueSoon').length,
      later: items.filter(i => i.state === 'Later').length,
    }
  })

  protected readonly visible = computed(() => {
    const focus = this.focus()
    const horizon = this.horizon()
    return this.scoped().filter(i => {
      switch (focus) {
        case 'expired':
          return i.state === 'Expired'
        case 'week':
          return this.inWeek(i)
        case 'soon':
          return i.state === 'DueSoon'
      }
      return (
        i.state !== 'Later' ||
        horizon === null ||
        (i.daysLeft !== null && i.daysLeft <= horizon)
      )
    })
  })

  /** Hidden by the period chips: said under the list so nothing seems lost. */
  protected readonly hiddenLater = computed(
    () =>
      this.scoped().filter(i => i.state !== 'Expired').length -
      this.visible().filter(i => i.state !== 'Expired').length
  )

  protected readonly groups = computed<Group[]>(() => {
    const groups: Group[] = []
    const byKey = new Map<string, Group>()
    const push = (
      key: string,
      kind: Group['kind'],
      month: string | null,
      item: ExpirationItem
    ) => {
      let group = byKey.get(key)
      if (!group) {
        group = { key, kind, month, items: [] }
        byKey.set(key, group)
        groups.push(group)
      }
      group.items.push(item)
    }
    // The list comes most urgent first: overdue, then by date, odometer-only ones after.
    for (const item of this.visible()) {
      if (item.state === 'Expired') {
        push('overdue', 'overdue', null, item)
      } else if (item.dueOn) {
        const month = item.dueOn.slice(0, 7) + '-01'
        push(month, 'month', month, item)
      } else {
        push('odometer', 'odometer', null, item)
      }
    }
    const order = (g: Group) =>
      g.kind === 'overdue' ? '0' : g.kind === 'odometer' ? '9' : '1' + g.month
    return groups.sort((a, b) => order(a).localeCompare(order(b)))
  })

  ngOnInit(): void {
    this.load()
  }

  protected toggleInactive(): void {
    this.showInactive.update(v => !v)
    this.load()
  }

  protected setFocus(focus: Focus): void {
    this.focus.set(this.focus() === focus ? 'all' : focus)
  }

  protected label(item: ExpirationItem): string {
    switch (item.source) {
      case 'DriverDocument':
        return item.documentType ? DOCUMENT_LABEL[item.documentType] : ''
      case 'VehicleDeadline':
        return item.title ?? ''
      default:
        return this.sourceLabel[item.source]
    }
  }

  protected ownerLink(kind: DeadlineOwner): string {
    return kind === 'Driver' ? '/admin/drivers' : '/admin/vehicles'
  }

  /** 0–100: how much of the alert window has gone (full bar = due today). */
  protected urgency(item: ExpirationItem): number {
    const d = this.data()
    if (!d) return 0
    const byDate =
      item.daysLeft === null
        ? 0
        : (d.alertDays - Math.max(item.daysLeft, 0)) / d.alertDays
    const byKm =
      item.kmLeft === null
        ? 0
        : (d.alertKm - Math.max(item.kmLeft, 0)) / d.alertKm
    return Math.round(Math.min(Math.max(byDate, byKm, 0.04), 1) * 100)
  }

  protected abs(n: number): number {
    return Math.abs(n)
  }

  // ── Renew ───────────────────────────────────────────────────────────────────

  protected renewSteps(item: ExpirationItem): Step[] {
    switch (item.source) {
      case 'Service':
        return [
          { label: $localize`+6 months`, months: 6 },
          { label: $localize`+1 year`, months: 12 },
        ]
      case 'VehicleDeadline':
        return [
          { label: $localize`+1 year`, months: 12 },
          ...(item.dueKm !== null
            ? [
                { label: $localize`+10,000 km`, km: 10_000 },
                { label: $localize`+30,000 km`, km: 30_000 },
              ]
            : []),
        ]
      default:
        return [
          { label: $localize`+1 year`, months: 12 },
          { label: $localize`+2 years`, months: 24 },
          { label: $localize`+5 years`, months: 60 },
          { label: $localize`+10 years`, months: 120 },
        ]
    }
  }

  protected openRenew(item: ExpirationItem): void {
    this.formError.set(null)
    this.form.reset({ dueOn: item.dueOn ?? '', dueKm: item.dueKm })
    this.renewing.set(item)
  }

  /** A step counts from the old deadline, or from today when that is already past. */
  protected applyStep(step: Step): void {
    const item = this.renewing()
    if (!item) return
    if (step.months) {
      const base =
        item.dueOn && item.dueOn > this.today ? item.dueOn : this.today
      this.form.controls.dueOn.setValue(addMonths(base, step.months))
    }
    if (step.km && item.dueKm !== null) {
      const mileage = item.dueKm - (item.kmLeft ?? 0)
      this.form.controls.dueKm.setValue(Math.max(item.dueKm, mileage) + step.km)
    }
  }

  protected saveRenew(): void {
    const item = this.renewing()
    if (!item || this.form.invalid) {
      this.form.markAllAsTouched()
      return
    }
    const v = this.form.getRawValue()
    const isDeadline = item.source === 'VehicleDeadline'
    if (!v.dueOn && !(isDeadline && v.dueKm !== null)) {
      this.formError.set(
        isDeadline
          ? $localize`Give a date or an odometer reading.`
          : $localize`Give the new expiry date.`
      )
      return
    }
    this.saving.set(true)
    this.api
      .renew({
        source: item.source,
        ownerId: item.ownerId,
        recordId: item.recordId,
        dueOn: v.dueOn || null,
        dueKm: isDeadline ? v.dueKm : null,
      })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.toast.success(
            $localize`${this.label(item)}:label: of ${item.ownerName}:owner: renewed.`
          )
          this.renewing.set(null)
          this.load()
        },
        error: err => this.formError.set(problemMessage(err)),
      })
  }

  // ── Export ──────────────────────────────────────────────────────────────────

  protected exportExcel(): void {
    const header = [
      $localize`Status`,
      $localize`Due date`,
      $localize`Days left`,
      $localize`Due at km`,
      $localize`Km left`,
      $localize`Deadline`,
      $localize`Who`,
      $localize`Name`,
      $localize`Details`,
      $localize`Number / notes`,
    ]
    const rows = this.visible().map(i => [
      this.stateLabel[i.state],
      i.dueOn ?? '',
      i.daysLeft ?? '',
      i.dueKm ?? '',
      i.kmLeft ?? '',
      this.label(i),
      i.ownerKind === 'Driver' ? $localize`Driver` : $localize`Vehicle`,
      i.ownerName,
      i.ownerDetail ?? '',
      i.reference ?? '',
    ])
    downloadCsv(`expirations-${this.today}.csv`, [header, ...rows])
  }

  private inWeek(item: ExpirationItem): boolean {
    return item.daysLeft !== null && item.daysLeft >= 0 && item.daysLeft <= 7
  }

  private searchText(item: ExpirationItem): string {
    return [item.ownerName, item.ownerDetail, this.label(item), item.reference]
      .filter(x => !!x)
      .join(' ')
      .toLowerCase()
  }

  private load(): void {
    this.loading.set(true)
    this.error.set(null)
    this.api
      .list(this.showInactive())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: d => this.data.set(d),
        error: err => this.error.set(problemMessage(err)),
      })
  }
}

/** "2026-01-31" + 1 month → "2026-02-28": the day is kept, or the month's last day. */
export function addMonths(date: string, months: number): string {
  const [y, m, d] = date.split('-').map(Number)
  const last = new Date(Date.UTC(y, m - 1 + months + 1, 0)).getUTCDate()
  const result = new Date(Date.UTC(y, m - 1 + months, Math.min(d, last)))
  return result.toISOString().slice(0, 10)
}
