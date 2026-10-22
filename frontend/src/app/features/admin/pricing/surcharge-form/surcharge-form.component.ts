import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  output,
} from '@angular/core'
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms'
import {
  DayOfWeek,
  SurchargeKind,
  SurchargeRule,
  SurchargeRuleRequest,
} from '@/core/models'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { FormFieldComponent } from '@/shared/components/form-field/form-field.component'
import { ADMIN_TEXT } from '@/features/admin/admin-texts'

export const DAYS: { value: DayOfWeek; label: string }[] = [
  { value: 'Monday', label: $localize`Mon` },
  { value: 'Tuesday', label: $localize`Tue` },
  { value: 'Wednesday', label: $localize`Wed` },
  { value: 'Thursday', label: $localize`Thu` },
  { value: 'Friday', label: $localize`Fri` },
  { value: 'Saturday', label: $localize`Sat` },
  { value: 'Sunday', label: $localize`Sun` },
]

export const KINDS: { value: SurchargeKind; label: string }[] = [
  { value: 'DaysOfWeek', label: $localize`Days of the week` },
  { value: 'DateRange', label: $localize`Dates (holidays, events, season)` },
  { value: 'TimeOfDay', label: $localize`Time of day` },
]

/** Fields needed by the chosen kind must be filled in. */
function kindFieldsValidator(group: AbstractControl): ValidationErrors | null {
  const v = group.value as SurchargeRuleRequest
  switch (v.kind) {
    case 'DaysOfWeek':
      return v.daysOfWeek.length ? null : { days: true }
    case 'DateRange':
      return v.startDate && v.endDate ? null : { dates: true }
    default:
      return v.startTime && v.endTime ? null : { times: true }
  }
}

@Component({
  selector: 'app-surcharge-form',
  imports: [ReactiveFormsModule, ButtonComponent, FormFieldComponent],
  templateUrl: './surcharge-form.component.html',
  styleUrl: './surcharge-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SurchargeFormComponent {
  protected readonly text = ADMIN_TEXT
  protected readonly tx = {
    nameIn: (language: string) => $localize`Name – ${language}:language:`,
  }

  readonly rule = input<SurchargeRule | null>(null)
  readonly saving = input(false)
  readonly saved = output<SurchargeRuleRequest>()
  readonly cancelled = output()

  protected readonly days = DAYS
  protected readonly kinds = KINDS
  protected readonly nameLanguages = [
    { code: 'It', name: $localize`Italian`, control: 'nameIt' },
    { code: 'De', name: $localize`German`, control: 'nameDe' },
    { code: 'Fr', name: $localize`French`, control: 'nameFr' },
  ] as const

  private readonly fb = inject(FormBuilder).nonNullable
  protected readonly form = this.fb.group(
    {
      name: ['', [Validators.required, Validators.maxLength(120)]],
      nameIt: ['', Validators.maxLength(120)],
      nameDe: ['', Validators.maxLength(120)],
      nameFr: ['', Validators.maxLength(120)],
      kind: this.fb.control<SurchargeKind>('DateRange'),
      percent: [
        10,
        [Validators.required, Validators.min(0), Validators.max(200)],
      ],
      daysOfWeek: this.fb.control<DayOfWeek[]>([]),
      startDate: [''],
      endDate: [''],
      startTime: [''],
      endTime: [''],
      repeatsYearly: [false],
      isActive: [true],
    },
    { validators: kindFieldsValidator }
  )

  constructor() {
    effect(() => {
      const rule = this.rule()
      this.form.reset({
        name: rule?.name ?? '',
        nameIt: rule?.nameTranslations?.it ?? '',
        nameDe: rule?.nameTranslations?.de ?? '',
        nameFr: rule?.nameTranslations?.fr ?? '',
        kind: rule?.kind ?? 'DateRange',
        percent: rule?.percent ?? 10,
        daysOfWeek: rule?.daysOfWeek ?? [],
        startDate: rule?.startDate ?? '',
        endDate: rule?.endDate ?? '',
        startTime: rule?.startTime?.slice(0, 5) ?? '',
        endTime: rule?.endTime?.slice(0, 5) ?? '',
        repeatsYearly: rule?.repeatsYearly ?? false,
        isActive: rule?.isActive ?? true,
      })
    })
  }

  protected toggleDay(day: DayOfWeek, checked: boolean): void {
    const control = this.form.controls.daysOfWeek
    const days = control.value.filter(d => d !== day)
    control.setValue(checked ? [...days, day] : days)
    control.markAsDirty()
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched()
      return
    }
    const { nameIt, nameDe, nameFr, ...v } = this.form.getRawValue()
    this.saved.emit({
      ...v,
      name: v.name.trim(),
      nameTranslations: {
        it: nameIt.trim() || null,
        de: nameDe.trim() || null,
        fr: nameFr.trim() || null,
      },
      startDate: v.startDate || null,
      endDate: v.endDate || null,
      startTime: v.startTime ? `${v.startTime}:00` : null,
      endTime: v.endTime ? `${v.endTime}:00` : null,
    })
  }
}
