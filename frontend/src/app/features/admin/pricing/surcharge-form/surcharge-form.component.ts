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

export const DAYS: { value: DayOfWeek; label: string }[] = [
  { value: 'Monday', label: 'Mon' },
  { value: 'Tuesday', label: 'Tue' },
  { value: 'Wednesday', label: 'Wed' },
  { value: 'Thursday', label: 'Thu' },
  { value: 'Friday', label: 'Fri' },
  { value: 'Saturday', label: 'Sat' },
  { value: 'Sunday', label: 'Sun' },
]

export const KINDS: { value: SurchargeKind; label: string }[] = [
  { value: 'DaysOfWeek', label: 'Days of the week' },
  { value: 'DateRange', label: 'Dates (holidays, events, season)' },
  { value: 'TimeOfDay', label: 'Time of day' },
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
  readonly rule = input<SurchargeRule | null>(null)
  readonly saving = input(false)
  readonly saved = output<SurchargeRuleRequest>()
  readonly cancelled = output()

  protected readonly days = DAYS
  protected readonly kinds = KINDS

  private readonly fb = inject(FormBuilder).nonNullable
  protected readonly form = this.fb.group(
    {
      name: ['', [Validators.required, Validators.maxLength(120)]],
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
    const v = this.form.getRawValue()
    this.saved.emit({
      ...v,
      name: v.name.trim(),
      startDate: v.startDate || null,
      endDate: v.endDate || null,
      startTime: v.startTime ? `${v.startTime}:00` : null,
      endTime: v.endTime ? `${v.endTime}:00` : null,
    })
  }
}
