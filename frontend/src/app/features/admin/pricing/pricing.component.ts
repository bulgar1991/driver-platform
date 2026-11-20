import {
  ChangeDetectionStrategy,
  Component,
  LOCALE_ID,
  OnInit,
  inject,
  signal,
} from '@angular/core'
import {
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms'
import { MatCheckbox } from '@angular/material/checkbox'
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field'
import { MatInput } from '@angular/material/input'
import {
  DialogComponent,
  DialogOpenDirective,
} from '@/shared/components/dialog'
import { finalize, Observable } from 'rxjs'
import {
  ExtraType,
  PricingOverview,
  SurchargeRule,
  SurchargeRuleRequest,
  VehicleCategory,
  VehicleClassNames,
  VehicleRate,
  VehicleRateRequest,
} from '@/core/models'
import { AdminPricingService } from '@/core/services/admin-pricing.service'
import { ToastService } from '@/core/services/toast.service'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import {
  DAYS,
  SurchargeFormComponent,
} from './surcharge-form/surcharge-form.component'
import { ADMIN_TEXT } from '@/features/admin/admin-texts'
import { TranslatedPipe } from '@/shared/pipes/translated.pipe'
import { CURRENCY_LABEL } from '@/core/currency'
import { siteLocale } from '@/core/i18n/locales'
import {
  pickName,
  VehicleClassService,
} from '@/core/services/vehicle-class.service'
import { DialogService } from '@/shared/components/dialog'

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
  ChildSeat: $localize`Child seat (each)`,
  ExtraLuggage: $localize`Extra large bag (beyond one per passenger)`,
  NameSign: $localize`Name sign`,
}

/** Business plan §4: tariffs per km, day surcharges and fixed extras, editable without code. */
@Component({
  selector: 'app-pricing',
  imports: [
    MatFormField,
    MatLabel,
    MatError,
    MatInput,
    MatCheckbox,
    DialogComponent,
    DialogOpenDirective,
    ReactiveFormsModule,
    ButtonComponent,
    IconComponent,
    SurchargeFormComponent,
    TranslatedPipe,
  ],
  templateUrl: './pricing.component.html',
  styleUrl: './pricing.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PricingComponent implements OnInit {
  /** "€" or "CHF", for the price labels. */
  protected readonly currency = CURRENCY_LABEL
  protected readonly tx = {
    baseFee: (category: string) => $localize`${category}:category: base fee`,
    pricePerKm: (category: string) =>
      $localize`${category}:category: price per km`,
    passengers: (category: string) =>
      $localize`${category}:category: passengers`,
    largeBags: (category: string) =>
      $localize`${category}:category: large bags`,
    edit: (name: string) => $localize`Edit ${name}:name:`,
    delete: (name: string) => $localize`Delete ${name}:name:`,
    editSurcharge: $localize`Edit surcharge`,
    newSurcharge: $localize`New surcharge`,
    newTariff: $localize`New tariff`,
    editTariff: (name: string) => $localize`Tariff ${name}:name:`,
    deleteTitle: (name: string) => $localize`Delete the ${name}:name: tariff`,
    deleteUnused: $localize`It is not used by any booking or car, so it is deleted for good, with its zone prices.`,
    deleteUsed: $localize`Bookings or cars use it, so it is hidden instead: no longer offered for new bookings, but past bookings, invoices and cars keep it. You can show it again later.`,
    deleteButton: $localize`Delete`,
    hideButton: $localize`Hide`,
    added: (name: string) => $localize`${name}:name: tariff added.`,
    deleted: (name: string) => $localize`${name}:name: tariff deleted.`,
    hidden: (name: string) =>
      $localize`${name}:name: tariff hidden: no longer offered for new bookings.`,
    shown: (name: string) => $localize`${name}:name: tariff shown again.`,
  }
  private readonly language = siteLocale(inject(LOCALE_ID))
    .code as keyof VehicleClassNames
  /** The name set here, or the built-in one (Standard, Business, VIP, Bus). */
  protected readonly categoryLabel = (category: VehicleCategory) => {
    const rate = this.rateList().find(r => r.category === category)
    return (
      pickName(rate?.names, this.language) ?? ADMIN_TEXT.categories[category]
    )
  }
  private readonly dialogs = inject(DialogService)
  private readonly vehicleClasses = inject(VehicleClassService)
  /** The tariffs as loaded: names, hidden or not, used or not. */
  protected readonly rateList = signal<VehicleRate[]>([])
  /** undefined: closed; null: a new tariff; a tariff: its names and figures. */
  protected readonly tariffDialog = signal<VehicleRate | null | undefined>(
    undefined
  )

  private readonly toast = inject(ToastService)
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

  protected readonly tariffForm = this.fb.group({
    nameEn: ['', Validators.maxLength(60)],
    nameFr: ['', Validators.maxLength(60)],
    nameDe: ['', Validators.maxLength(60)],
    nameIt: ['', Validators.maxLength(60)],
    baseFee: [
      10,
      [Validators.required, Validators.min(0), Validators.max(1000)],
    ],
    pricePerKm: [
      2,
      [Validators.required, Validators.min(0), Validators.max(100)],
    ],
    maxPassengers: [
      4,
      [Validators.required, Validators.min(1), Validators.max(50)],
    ],
    maxLuggage: [
      4,
      [Validators.required, Validators.min(0), Validators.max(60)],
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
      this.notify(
        $localize`${this.categoryLabel(category)}:category: tariff saved.`
      )
    })
  }

  /** The popup for a new tariff, or for a tariff's names and figures. */
  protected openTariff(rate: VehicleRate | null): void {
    const nameEn = this.tariffForm.controls.nameEn
    // New and added tariffs need an English name; the built-in ones have theirs.
    nameEn.setValidators(
      rate?.isBuiltIn
        ? [Validators.maxLength(60)]
        : [Validators.required, Validators.maxLength(60)]
    )
    this.tariffForm.reset({
      nameEn: rate?.names.en ?? '',
      nameFr: rate?.names.fr ?? '',
      nameDe: rate?.names.de ?? '',
      nameIt: rate?.names.it ?? '',
      baseFee: rate?.baseFee ?? 10,
      pricePerKm: rate?.pricePerKm ?? 2,
      maxPassengers: rate?.maxPassengers ?? 4,
      maxLuggage: rate?.maxLuggage ?? 4,
    })
    this.tariffDialog.set(rate)
  }

  protected saveTariff(): void {
    if (this.tariffForm.invalid) {
      this.tariffForm.markAllAsTouched()
      return
    }
    const v = this.tariffForm.getRawValue()
    const request: VehicleRateRequest = {
      baseFee: v.baseFee,
      pricePerKm: v.pricePerKm,
      maxPassengers: v.maxPassengers,
      maxLuggage: v.maxLuggage,
      names: { en: v.nameEn, it: v.nameIt, de: v.nameDe, fr: v.nameFr },
    }
    const rate = this.tariffDialog()
    const call = rate
      ? this.api.updateRate(rate.category, request)
      : this.api.createRate(request)
    this.run('tariff', call, saved => {
      this.tariffDialog.set(undefined)
      this.reload()
      this.toast.success(
        rate
          ? $localize`${this.categoryLabel(saved.category)}:category: tariff saved.`
          : this.tx.added(v.nameEn)
      )
    })
  }

  /** Asks first; a tariff in use is hidden, not deleted. */
  protected deleteTariff(rate: VehicleRate): void {
    const name = this.categoryLabel(rate.category)
    this.dialogs
      .confirm({
        title: this.tx.deleteTitle(name),
        message: rate.inUse ? this.tx.deleteUsed : this.tx.deleteUnused,
        confirm: rate.inUse ? this.tx.hideButton : this.tx.deleteButton,
        danger: true,
      })
      .subscribe(ok => {
        if (!ok) {
          return
        }
        this.run(
          `rate-${rate.category}`,
          this.api.deleteRate(rate.category),
          result => {
            this.fillRates(result.rates)
            this.vehicleClasses.load().subscribe({ error: () => undefined })
            this.toast.success(
              result.deleted ? this.tx.deleted(name) : this.tx.hidden(name)
            )
          }
        )
      })
  }

  protected showTariff(rate: VehicleRate): void {
    this.run(
      `rate-${rate.category}`,
      this.api.setRateActive(rate.category, true),
      () => {
        this.reload()
        this.toast.success(this.tx.shown(this.categoryLabel(rate.category)))
      }
    )
  }

  protected rateOf(category: VehicleCategory): VehicleRate | undefined {
    return this.rateList().find(r => r.category === category)
  }

  private reload(): void {
    this.api.get().subscribe({
      next: overview => this.fillRates(overview.rates),
      error: err => this.notify(problemMessage(err), true),
    })
    this.vehicleClasses.load().subscribe({ error: () => undefined })
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
      this.notify($localize`${EXTRA_LABELS[type]} saved.`)
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
        this.notify($localize`Settings saved.`)
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
      this.notify($localize`“${saved.name}” saved.`)
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
      this.notify($localize`“${rule.name}” deleted.`)
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
            ? new Date(`${date}T12:00:00`).toLocaleDateString(
                $localize.locale ?? 'en-GB',
                {
                  day: 'numeric',
                  month: 'short',
                  ...(rule.repeatsYearly ? {} : { year: 'numeric' }),
                }
              )
            : '?'
        const range =
          rule.startDate === rule.endDate
            ? format(rule.startDate)
            : `${format(rule.startDate)} – ${format(rule.endDate)}`
        return rule.repeatsYearly
          ? $localize`${range}:range:, every year`
          : range
      }
      default:
        return `${rule.startTime?.slice(0, 5)}–${rule.endTime?.slice(0, 5)}`
    }
  }

  private fill(overview: PricingOverview): void {
    this.fillRates(overview.rates)
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

  private fillRates(rates: VehicleRate[]): void {
    this.rateList.set(rates)
    this.rates.clear()
    for (const rate of rates) {
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
            [Validators.required, Validators.min(1), Validators.max(50)],
          ],
          maxLuggage: [
            rate.maxLuggage,
            [Validators.required, Validators.min(0), Validators.max(60)],
          ],
        })
      )
    }
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

  /** Confirmations come and go as a toast; errors stay on the page until the next action. */
  private notify(text: string, error = false): void {
    if (error) {
      this.message.set({ text, error })
    } else {
      this.message.set(null)
      this.toast.success(text)
    }
  }
}
