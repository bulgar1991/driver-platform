import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from '@angular/core'
import { ReactiveFormsModule } from '@angular/forms'
import { BookingOptions } from '@/core/models'
import { todayInItaly } from '@/core/utils/italy-time'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { CounterComponent } from '@/shared/components/counter/counter.component'
import { FormFieldComponent } from '@/shared/components/form-field/form-field.component'
import { AddressInputComponent } from '../address-input/address-input.component'
import { TripForm } from '../../booking-form'

/** Step 1: where, when, how many. */
@Component({
  selector: 'app-trip-step',
  imports: [
    ReactiveFormsModule,
    AddressInputComponent,
    ButtonComponent,
    CounterComponent,
    FormFieldComponent,
  ],
  templateUrl: './trip-step.component.html',
  styleUrl: './trip-step.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TripStepComponent {
  readonly form = input.required<TripForm>()
  readonly options = input<BookingOptions | null>(null)
  readonly loading = input(false)
  readonly submitted = output()

  protected readonly minDate = todayInItaly()

  protected invalid(name: keyof TripForm['controls']): boolean {
    const control = this.form().controls[name]
    return control.invalid && control.touched
  }

  protected get returnMissing(): boolean {
    const form = this.form()
    return form.hasError('returnRequired') && form.controls.returnDate.touched
  }

  protected submit(): void {
    const form = this.form()
    if (form.invalid) {
      form.markAllAsTouched()
      return
    }
    this.submitted.emit()
  }
}
