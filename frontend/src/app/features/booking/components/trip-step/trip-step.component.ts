import { ChangeDetectionStrategy, Component, input } from '@angular/core'
import { ReactiveFormsModule } from '@angular/forms'
import { BookingOptions } from '@/core/models'
import { todayInItaly } from '@/core/utils/italy-time'
import { CounterComponent } from '@/shared/components/counter/counter.component'
import { FormFieldComponent } from '@/shared/components/form-field/form-field.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { AddressInputComponent } from '../address-input/address-input.component'
import { TripForm } from '../../booking-form'

/** From / to, then when and how many. The price updates on its own as the form changes. */
@Component({
  selector: 'app-trip-step',
  imports: [
    ReactiveFormsModule,
    AddressInputComponent,
    CounterComponent,
    FormFieldComponent,
    IconComponent,
  ],
  templateUrl: './trip-step.component.html',
  styleUrl: './trip-step.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TripStepComponent {
  readonly form = input.required<TripForm>()
  readonly options = input<BookingOptions | null>(null)

  protected readonly minDate = todayInItaly()

  protected invalid(name: keyof TripForm['controls']): boolean {
    const control = this.form().controls[name]
    return control.invalid && control.touched
  }

  protected get returnMissing(): boolean {
    const form = this.form()
    return form.hasError('returnRequired') && form.controls.returnDate.touched
  }

  /** Swaps the two addresses (and their meeting points). */
  protected swap(): void {
    const { pickup, dropoff } = this.form().getRawValue()
    this.form().patchValue({
      pickup: dropoff,
      dropoff: pickup,
      pickupMeetingPoint: null,
      dropoffMeetingPoint: null,
    })
  }
}
