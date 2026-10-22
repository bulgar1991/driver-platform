import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  forwardRef,
  inject,
  input,
  signal,
  viewChild,
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
import {
  catchError,
  debounceTime,
  filter,
  map,
  of,
  Subject,
  switchMap,
} from 'rxjs'
import { GeoPoint, Place } from '@/core/models'
import { GeoService } from '@/core/services/geo.service'
import { IconComponent } from '@/shared/components/icon/icon.component'

/** Address search in the countries served (PrimeNG AutoComplete). The form value is a GeoPoint, or null until one is picked. */
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
  readonly placeholder = input($localize`Address, hotel, airport…`)
  readonly invalid = input(false)

  private readonly geo = inject(GeoService)
  private readonly cdr = inject(ChangeDetectorRef)
  private readonly autoComplete = viewChild(AutoComplete)
  private readonly queries = new Subject<string>()

  protected readonly suggestions = signal<Place[]>([])
  /** The text the current suggestions were found for. */
  private suggestionsFor = ''
  protected readonly disabled = signal(false)
  /** What the AutoComplete shows: the picked place, or the text being typed. */
  protected selection: Place | string | null = null

  private onChange: (value: GeoPoint | null) => void = () => undefined
  protected onTouched: () => void = () => undefined

  constructor() {
    this.queries
      .pipe(
        debounceTime(300),
        switchMap(q =>
          this.geo.search(q).pipe(
            catchError(() => of([])),
            map(places => ({ q, places }))
          )
        ),
        takeUntilDestroyed(inject(DestroyRef))
      )
      .subscribe(({ q, places }) => {
        // An answer that comes back after a place was chosen would reopen the list.
        if (typeof this.selection !== 'string') {
          return
        }
        this.suggestionsFor = q
        this.suggestions.set(places)
      })
  }

  /**
   * Typed a city or an address but did not pick from the list (left the field, pressed Enter):
   * the best match is taken, so "Roma" or "Milano" just works.
   */
  protected resolveTyped(): void {
    this.onTouched()
    const text = typeof this.selection === 'string' ? this.selection.trim() : ''
    if (text.length < 3) {
      return
    }
    const pick = (places: Place[]) => {
      const best = bestMatch(text, places)
      // Still the same text: the user has not typed on in the meantime.
      if (
        best &&
        typeof this.selection === 'string' &&
        this.selection.trim() === text
      ) {
        this.choose(best)
      }
    }
    if (this.suggestionsFor === text) {
      pick(this.suggestions())
    } else {
      this.geo
        .search(text)
        .pipe(
          catchError(() => of([])),
          filter(places => places.length > 0)
        )
        .subscribe(pick)
    }
  }

  protected keydown(event: Event): void {
    // Enter with nothing highlighted (list still loading): resolve the text instead.
    if (
      (event as KeyboardEvent).key === 'Enter' &&
      typeof this.selection === 'string'
    ) {
      this.resolveTyped()
    }
  }

  protected search(event: AutoCompleteCompleteEvent): void {
    this.queries.next(event.query.trim())
  }

  protected select(event: AutoCompleteSelectEvent): void {
    this.choose(event.value as Place)
  }

  private choose(place: Place): void {
    this.selection = place
    // Close the list. Emptying it instead would make PrimeNG open "No address found".
    this.autoComplete()?.hide()
    this.cdr.markForCheck()
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
    // Set from outside (a click on the map, swapping From and To): show it now.
    this.cdr.markForCheck()
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

/** A meeting point whose name starts with the text (an airport, a station), else the top address. */
export function bestMatch(text: string, places: Place[]): Place | undefined {
  const query = normalize(text)
  return (
    places.find(
      p => p.kind === 'MeetingPoint' && normalize(p.label).startsWith(query)
    ) ??
    places.find(p => p.kind !== 'MeetingPoint') ??
    places[0]
  )
}

function normalize(value: string): string {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .trim()
}
