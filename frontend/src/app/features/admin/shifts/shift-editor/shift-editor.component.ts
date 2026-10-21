import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core'
import { FormsModule } from '@angular/forms'
import { finalize, Subscription } from 'rxjs'
import {
  Availability,
  BookingListItem,
  Shift,
  ShiftResult,
} from '@/core/models'
import { AdminDispatchService } from '@/core/services/admin-dispatch.service'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'

/**
 * Builds or edits a shift: tick bookings, then pick a driver and a car. The API says who is free
 * and why the others are not; it re-checks everything on save.
 */
@Component({
  selector: 'app-shift-editor',
  imports: [DatePipe, FormsModule, ButtonComponent],
  templateUrl: './shift-editor.component.html',
  styleUrl: './shift-editor.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShiftEditorComponent {
  /** null: a new shift. */
  readonly shift = input<Shift | null>(null)
  /** Bookings of the day still to assign. */
  readonly candidates = input<BookingListItem[]>([])
  readonly preselected = input<string[]>([])
  readonly saved = output<ShiftResult>()
  readonly closed = output()

  private readonly api = inject(AdminDispatchService)
  private availabilityCall?: Subscription

  protected readonly selected = signal<ReadonlySet<string>>(new Set())
  protected readonly availability = signal<Availability | null>(null)
  protected readonly driverId = signal('')
  protected readonly vehicleId = signal('')
  protected notes = ''
  protected readonly saving = signal(false)
  protected readonly error = signal<string | null>(null)

  /** The shift's own bookings first, then the ones still to assign. */
  protected readonly options = computed(() => {
    const own = this.shift()?.bookings ?? []
    const ownIds = new Set(own.map(b => b.id))
    return [...own, ...this.candidates().filter(b => !ownIds.has(b.id))].sort(
      (a, b) => a.pickupAt.localeCompare(b.pickupAt)
    )
  })

  protected readonly driverProblems = computed(
    () =>
      this.availability()?.drivers.find(d => d.id === this.driverId())
        ?.problems ?? []
  )
  protected readonly vehicleProblems = computed(
    () =>
      this.availability()?.vehicles.find(v => v.id === this.vehicleId())
        ?.problems ?? []
  )

  constructor() {
    effect(() => {
      const shift = this.shift()
      const preselected = this.preselected()
      untracked(() => {
        this.selected.set(
          new Set(shift ? shift.bookings.map(b => b.id) : preselected)
        )
        this.driverId.set(shift?.driverId ?? '')
        this.vehicleId.set(shift?.vehicleId ?? '')
        this.notes = shift?.notes ?? ''
        this.error.set(null)
        this.loadAvailability()
      })
    })
  }

  protected toggle(id: string): void {
    const next = new Set(this.selected())
    if (next.has(id)) {
      next.delete(id)
    } else {
      next.add(id)
    }
    this.selected.set(next)
    this.loadAvailability()
  }

  protected save(): void {
    if (!this.selected().size || !this.driverId() || !this.vehicleId()) {
      this.error.set('Choose at least one booking, a driver and a car.')
      return
    }
    const request = {
      driverId: this.driverId(),
      vehicleId: this.vehicleId(),
      bookingIds: [...this.selected()],
      notes: this.notes.trim() || null,
    }
    const shift = this.shift()
    this.saving.set(true)
    this.error.set(null)
    ;(shift
      ? this.api.updateShift(shift.id, request)
      : this.api.createShift(request)
    )
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: result => this.saved.emit(result),
        error: err => this.error.set(problemMessage(err)),
      })
  }

  private loadAvailability(): void {
    this.availabilityCall?.unsubscribe()
    const ids = [...this.selected()]
    if (!ids.length) {
      this.availability.set(null)
      return
    }
    this.availabilityCall = this.api
      .availability(ids, this.shift()?.id)
      .subscribe({
        next: a => {
          this.availability.set(a)
          // Suggest the first free driver and the smallest free car when nothing is chosen yet.
          if (!this.driverId()) {
            this.driverId.set(a.drivers.find(d => d.available)?.id ?? '')
          }
          if (!this.vehicleId()) {
            this.vehicleId.set(a.vehicles.find(v => v.available)?.id ?? '')
          }
        },
        error: err => this.error.set(problemMessage(err)),
      })
  }
}
