import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core'
import {
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms'
import { Dialog } from 'primeng/dialog'
import { finalize, Observable } from 'rxjs'
import {
  ExtraType,
  PricingOverview,
  SurchargeRule,
  SurchargeRuleRequest,
  VehicleCategory,
} from '@/core/models'
import { AdminPricingService } from '@/core/services/admin-pricing.service'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import {
  DAYS,
  SurchargeFormComponent,
} from './surcharge-form/surcharge-form.component'

type RateForm = FormGroup<{
  category: FormControl<VehicleCategory>
  baseFee: FormControl<number>
  pricePerKm: FormControl<number>
  maxPassengers: FormControl<number>
  maxLuggage: FormControl<number>
}>

type ExtraForm = FormGroup<{
  type: FormControl<ExtraType>
  amount: FormControl<number>
}>

const EXTRA_LABELS: Record<ExtraType, string> = {
  ChildSeat: 'Child seat (each)',
  ExtraLuggage: 'Extra large bag (beyond one per passenger)',
  NameSign: 'Name sign',
}

/** Business plan §4: tariffs per km, day surcharges and fixed extras, editable without code. */
@Component({
  selector: 'app-pricing',
  imports: [
    ReactiveFormsModule,
    Dialog,
    ButtonComponent,
    IconComponent,
    SurchargeFormComponent,
  ],
  templateUrl: './pricing.component.html',
  styleUrl: './pricing.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PricingComponent implements OnInit {
  private readonly api = inject(AdminPricingService)
  private readonly fb = inject(FormBuilder).nonNullable

  protected readonly extraLabels = EXTRA_LABELS
  protected readonly surcharges = signal<SurchargeRule[]>([])
  protected readonly loading = signal(true)
  protected readonly message = signal<{ text: string; error: boolean } | null>(
    null
  )
  protected readonly saving = signal<string | null>(null)

  protected readonly dialogOpen = signal(false)
  protected readonly editing = signal<SurchargeRule | null>(null)
  protected readonly confirmDelete = signal<string | null>(null)

  protected readonly rates = this.fb.array<RateForm>([])
  protected readonly extras = this.fb.array<ExtraForm>([])
  protected readonly settings = this.fb.group({
    vatRatePercent: [
      10,
      [Validators.required, Validators.min(0), Validators.max(50)],
    ],
    returnDiscountPercent: [
      0,
      [Validators.required, Validators.min(0), Validators.max(50)],
    ],
    minLeadTimeHours: [
      2,
      [Validators.required, Validators.min(0), Validators.max(168)],
    ],
  })

  ngOnInit(): void {
    this.api
      .get()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: overview => this.fill(overview),
        error: err => this.notify(problemMessage(err), true),
      })
  }

  protected saveRate(index: number): void {
    const group = this.rates.at(index)
    if (group.invalid) {
      group.markAllAsTouched()
      return
    }
    const { category, ...rate } = group.getRawValue()
    this.run(`rate-${category}`, this.api.updateRate(category, rate), () => {
      group.markAsPristine()
      this.notify(`${category} tariff saved.`)
    })
  }

  protected saveExtra(index: number): void {
    const group = this.extras.at(index)
    if (group.invalid) {
      group.markAllAsTouched()
      return
    }
    const { type, amount } = group.getRawValue()
    this.run(`extra-${type}`, this.api.updateExtra(type, amount), () => {
      group.markAsPristine()
      this.notify(`${EXTRA_LABELS[type]} saved.`)
    })
  }

  protected saveSettings(): void {
    if (this.settings.invalid) {
      this.settings.markAllAsTouched()
      return
    }
    this.run(
      'settings',
      this.api.updateSettings(this.settings.getRawValue()),
      () => {
        this.settings.markAsPristine()
        this.notify('Settings saved.')
      }
    )
  }

  protected openSurcharge(rule: SurchargeRule | null): void {
    this.editing.set(rule)
    this.dialogOpen.set(true)
  }

  protected saveSurcharge(request: SurchargeRuleRequest): void {
    const rule = this.editing()
    const call = rule
      ? this.api.updateSurcharge(rule.id, request)
      : this.api.createSurcharge(request)
    this.run('surcharge', call, saved => {
      this.surcharges.update(list =>
        rule ? list.map(r => (r.id === saved.id ? saved : r)) : [...list, saved]
      )
      this.dialogOpen.set(false)
      this.notify(`“${saved.name}” saved.`)
    })
  }

  protected toggleSurcharge(rule: SurchargeRule): void {
    const { id, ...request } = rule
    this.run(
      `toggle-${id}`,
      this.api.updateSurcharge(id, { ...request, isActive: !rule.isActive }),
      saved =>
        this.surcharges.update(list =>
          list.map(r => (r.id === saved.id ? saved : r))
        )
    )
  }

  protected deleteSurcharge(rule: SurchargeRule): void {
    this.run(`delete-${rule.id}`, this.api.deleteSurcharge(rule.id), () => {
      this.surcharges.update(list => list.filter(r => r.id !== rule.id))
      this.confirmDelete.set(null)
      this.notify(`“${rule.name}” deleted.`)
    })
  }

  /** "Fri, Sat, Sun", "25 Dec (every year)", "22:00–06:00". */
  protected describe(rule: SurchargeRule): string {
    switch (rule.kind) {
      case 'DaysOfWeek':
        return DAYS.filter(d => rule.daysOfWeek.includes(d.value))
          .map(d => d.label)
          .join(', ')
      case 'DateRange': {
        const format = (date: string | null) =>
          date
            ? new Date(`${date}T12:00:00`).toLocaleDateString('en-GB', {
                day: 'numeric',
                month: 'short',
                ...(rule.repeatsYearly ? {} : { year: 'numeric' }),
              })
            : '?'
        const range =
          rule.startDate === rule.endDate
            ? format(rule.startDate)
            : `${format(rule.startDate)} – ${format(rule.endDate)}`
        return rule.repeatsYearly ? `${range}, every year` : range
      }
      default:
        return `${rule.startTime?.slice(0, 5)}–${rule.endTime?.slice(0, 5)}`
    }
  }

  private fill(overview: PricingOverview): void {
    this.rates.clear()
    for (const rate of overview.rates) {
      this.rates.push(
        this.fb.group({
          category: this.fb.control<VehicleCategory>(rate.category),
          baseFee: [
            rate.baseFee,
            [Validators.required, Validators.min(0), Validators.max(1000)],
          ],
          pricePerKm: [
            rate.pricePerKm,
            [Validators.required, Validators.min(0), Validators.max(100)],
          ],
          maxPassengers: [
            rate.maxPassengers,
            [Validators.required, Validators.min(1), Validators.max(8)],
          ],
          maxLuggage: [
            rate.maxLuggage,
            [Validators.required, Validators.min(0), Validators.max(16)],
          ],
        })
      )
    }
    this.extras.clear()
    for (const extra of overview.extras) {
      this.extras.push(
        this.fb.group({
          type: this.fb.control<ExtraType>(extra.type),
          amount: [
            extra.amount,
            [Validators.required, Validators.min(0), Validators.max(500)],
          ],
        })
      )
    }
    this.settings.reset(overview.settings)
    this.surcharges.set(overview.surcharges)
  }

  private run<T>(
    key: string,
    call: Observable<T>,
    done: (value: T) => void
  ): void {
    this.saving.set(key)
    this.message.set(null)
    call.pipe(finalize(() => this.saving.set(null))).subscribe({
      next: done,
      error: err => this.notify(problemMessage(err), true),
    })
  }

  private notify(text: string, error = false): void {
    this.message.set({ text, error })
  }
}
