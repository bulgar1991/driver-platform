import { ChangeDetectionStrategy, Component, input } from '@angular/core'

/** Label + control + error. Put an <input class="input"> inside. */
@Component({
  selector: 'app-form-field',
  templateUrl: './form-field.component.html',
  styleUrl: './form-field.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FormFieldComponent {
  readonly label = input.required<string>()
  readonly for = input.required<string>()
  readonly error = input<string | null>(null)
}
