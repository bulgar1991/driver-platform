import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from '@angular/core'
import { ReactiveFormsModule } from '@angular/forms'
import { RouterLink } from '@angular/router'
import { BookingOptions, VehicleOption } from '@/core/models'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { FormFieldComponent } from '@/shared/components/form-field/form-field.component'
import { MoneyPipe } from '@/shared/pipes/money.pipe'
import { ContactForm, DetailsForm } from '../../booking-form'

/** Step 3: options for the driver and the client's contact details. */
@Component({
  selector: 'app-details-step',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    ButtonComponent,
    FormFieldComponent,
    MoneyPipe,
  ],
  templateUrl: './details-step.component.html',
  styleUrl: './details-step.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DetailsStepComponent {
  readonly details = input.required<DetailsForm>()
  readonly contact = input.required<ContactForm>()
  readonly options = input<BookingOptions | null>(null)
  readonly vehicle = input<VehicleOption | null>(null)
  readonly hasReturn = input(false)
  readonly loading = input(false)
  readonly back = output()
  readonly submitted = output()

  protected detailsError(name: keyof DetailsForm['controls']): string | null {
    const control = this.details().controls[name]
    if (!control.touched || control.valid) {
      return null
    }
    return control.hasError('pattern')
      ? $localize`Letters and digits only, e.g. FR 1234`
      : $localize`Too long`
  }

  protected contactError(name: keyof ContactForm['controls']): string | null {
    const control = this.contact().controls[name]
    if (!control.touched || control.valid) {
      return null
    }
    if (control.hasError('required')) {
      return $localize`Required`
    }
    if (control.hasError('email')) {
      return $localize`Enter a valid email`
    }
    return control.hasError('pattern')
      ? $localize`Enter a phone number with the country code, e.g. +39 333 123 4567`
      : $localize`Too long`
  }

  /** The form spans two form groups, so it handles the native submit itself. */
  protected submit(event: Event): void {
    event.preventDefault()
    const details = this.details()
    const contact = this.contact()
    if (details.invalid || contact.invalid) {
      details.markAllAsTouched()
      contact.markAllAsTouched()
      return
    }
    this.submitted.emit()
  }
}
