import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  signal,
} from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { RouterLink } from '@angular/router'
import { finalize } from 'rxjs'
import { DriverLicenceKind } from '@/core/models'
import { SiteService } from '@/core/services/site.service'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { CounterComponent } from '@/shared/components/counter/counter.component'
import { FormFieldComponent } from '@/shared/components/form-field/form-field.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { RevealDirective } from '@/shared/directives/reveal.directive'
import { DRIVER_BENEFITS, LANGUAGES } from '../../home.content'

const LICENCES: { value: DriverLicenceKind; label: string; hint: string }[] = [
  { value: 'Ncc', label: 'NCC licence', hint: 'Noleggio con conducente' },
  { value: 'Taxi', label: 'Taxi licence', hint: 'Licenza taxi' },
  {
    value: 'ProfessionalOnly',
    label: 'CAP / KB only',
    hint: 'Professional certificate, no licence yet',
  },
]

/** "Do you want to earn with us?" — benefits and the application form. */
@Component({
  selector: 'app-drivers-join',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    ButtonComponent,
    CounterComponent,
    FormFieldComponent,
    IconComponent,
    RevealDirective,
  ],
  templateUrl: './drivers-join.component.html',
  styleUrl: './drivers-join.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DriversJoinComponent {
  /** The section is the whole page. */
  readonly page = input(false)
  private readonly site = inject(SiteService)
  private readonly fb = inject(FormBuilder).nonNullable

  protected readonly benefits = DRIVER_BENEFITS
  protected readonly licences = LICENCES
  protected readonly languages = LANGUAGES
  protected readonly sending = signal(false)
  protected readonly sent = signal(false)
  protected readonly error = signal<string | null>(null)

  protected readonly form = this.fb.group({
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    phone: [
      '',
      [Validators.required, Validators.pattern(/^\+?[0-9 ()-]{6,20}$/)],
    ],
    email: ['', [Validators.required, Validators.email]],
    city: ['', [Validators.required, Validators.maxLength(80)]],
    licence: this.fb.control<DriverLicenceKind>('Ncc'),
    yearsExperience: [3],
    hasOwnVehicle: [false],
    vehicle: ['', Validators.maxLength(120)],
    languages: this.fb.control<string[]>(['it']),
    message: ['', Validators.maxLength(2000)],
    privacyConsent: [false, Validators.requiredTrue],
    website: [''],
  })

  protected invalid(name: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[name]
    return control.invalid && control.touched
  }

  protected toggleLanguage(code: string): void {
    const control = this.form.controls.languages
    control.setValue(
      control.value.includes(code)
        ? control.value.filter(c => c !== code)
        : [...control.value, code]
    )
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched()
      return
    }
    const v = this.form.getRawValue()
    this.sending.set(true)
    this.error.set(null)
    this.site
      .applyAsDriver({
        ...v,
        vehicle: v.hasOwnVehicle ? v.vehicle.trim() || null : null,
        message: v.message.trim() || null,
      })
      .pipe(finalize(() => this.sending.set(false)))
      .subscribe({
        next: () => this.sent.set(true),
        error: err => this.error.set(problemMessage(err)),
      })
  }
}
