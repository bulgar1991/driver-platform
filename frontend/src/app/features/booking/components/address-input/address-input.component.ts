import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  forwardRef,
  inject,
  input,
  signal,
} from '@angular/core'
import { takeUntilDestroyed } from '@angular/core/rxjs-interop'
import {
  ControlValueAccessor,
  FormsModule,
  NG_VALUE_ACCESSOR,
} from '@angular/forms'
import {
  AutoComplete,
  AutoCompleteCompleteEvent,
  AutoCompleteSelectEvent,
} from 'primeng/autocomplete'
import { catchError, debounceTime, of, Subject, switchMap } from 'rxjs'
import { GeoPoint, Place } from '@/core/models'
import { GeoService } from '@/core/services/geo.service'
import { IconComponent } from '@/shared/components/icon/icon.component'

/** Address search in Italy (PrimeNG AutoComplete). The form value is a GeoPoint, or null until one is picked. */
@Component({
  selector: 'app-address-input',
  imports: [FormsModule, AutoComplete, IconComponent],
  templateUrl: './address-input.component.html',
  styleUrl: './address-input.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => AddressInputComponent),
      multi: true,
    },
  ],
})
export class AddressInputComponent implements ControlValueAccessor {
  readonly inputId = input.required<string>()
  readonly placeholder = input('Address, hotel, airport…')
  readonly invalid = input(false)

  private readonly geo = inject(GeoService)
  private readonly queries = new Subject<string>()

  protected readonly suggestions = signal<Place[]>([])
  protected readonly disabled = signal(false)
  /** What the AutoComplete shows: the picked place, or the text being typed. */
  protected selection: Place | string | null = null

  private onChange: (value: GeoPoint | null) => void = () => undefined
  protected onTouched: () => void = () => undefined

  constructor() {
    this.queries
      .pipe(
        debounceTime(300),
        switchMap(q => this.geo.search(q).pipe(catchError(() => of([])))),
        takeUntilDestroyed(inject(DestroyRef))
      )
      .subscribe(places => this.suggestions.set(places))
  }

  protected search(event: AutoCompleteCompleteEvent): void {
    this.queries.next(event.query.trim())
  }

  protected select(event: AutoCompleteSelectEvent): void {
    const place = event.value as Place
    this.onChange({
      address: place.kind === 'MeetingPoint' ? place.label : place.address,
      latitude: place.latitude,
      longitude: place.longitude,
    })
  }

  /** Typing after a pick clears the value until a new place is chosen. */
  protected typed(value: Place | string | null): void {
    this.selection = value
    if (typeof value === 'string' || value === null) {
      this.onChange(null)
    }
  }

  writeValue(value: GeoPoint | null): void {
    this.selection = value
      ? { ...value, label: value.address, kind: 'Address' }
      : null
  }

  registerOnChange(fn: (value: GeoPoint | null) => void): void {
    this.onChange = fn
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn
  }

  setDisabledState(disabled: boolean): void {
    this.disabled.set(disabled)
  }
}
