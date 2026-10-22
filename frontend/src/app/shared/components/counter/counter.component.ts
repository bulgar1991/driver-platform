import {
  ChangeDetectionStrategy,
  Component,
  forwardRef,
  input,
  signal,
} from '@angular/core'
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms'
import { IconComponent } from '../icon/icon.component'

/** − value + stepper for small counts (passengers, bags, seats). Works with reactive forms. */
@Component({
  selector: 'app-counter',
  imports: [IconComponent],
  templateUrl: './counter.component.html',
  styleUrl: './counter.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => CounterComponent),
      multi: true,
    },
  ],
})
export class CounterComponent implements ControlValueAccessor {
  protected readonly fewer = (label: string) => $localize`Fewer ${label}:label:`
  protected readonly more = (label: string) => $localize`More ${label}:label:`
  readonly label = input.required<string>()
  readonly hint = input<string | null>(null)
  readonly min = input(0)
  readonly max = input(8)

  protected readonly value = signal(0)
  protected readonly disabled = signal(false)
  private onChange: (value: number) => void = () => undefined
  private onTouched: () => void = () => undefined

  protected step(delta: number): void {
    const next = Math.min(
      this.max(),
      Math.max(this.min(), this.value() + delta)
    )
    if (next !== this.value()) {
      this.value.set(next)
      this.onChange(next)
    }
    this.onTouched()
  }

  writeValue(value: number | null): void {
    this.value.set(value ?? this.min())
  }

  registerOnChange(fn: (value: number) => void): void {
    this.onChange = fn
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn
  }

  setDisabledState(disabled: boolean): void {
    this.disabled.set(disabled)
  }
}
