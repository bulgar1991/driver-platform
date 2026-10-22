import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
} from '@angular/core'
import {
  DriverBookingView,
  LatLng,
  MapMarker,
  BookingStatus,
} from '@/core/models'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { MapComponent } from '@/shared/components/map/map.component'
import { StatusBadgeComponent } from '@/shared/components/status-badge/status-badge.component'
import { DurationPipe } from '@/shared/pipes/duration.pipe'
import { TranslatedPipe } from '@/shared/pipes/translated.pipe'

const SEAT_LABEL = {
  Infant: $localize`infant seat 0+`,
  Toddler: $localize`child seat gr. 1`,
  Booster: $localize`booster gr. 2/3`,
}

export type RideAction = 'pickedUp' | 'completed'

/** One ride on the driver's page: everything chosen by the client, unchanged (business plan §3). */
@Component({
  selector: 'app-driver-ride',
  imports: [
    DatePipe,
    TranslatedPipe,
    ButtonComponent,
    IconComponent,
    MapComponent,
    StatusBadgeComponent,
    DurationPipe,
  ],
  templateUrl: './driver-ride.component.html',
  styleUrl: './driver-ride.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DriverRideComponent {
  readonly ride = input.required<DriverBookingView>()
  readonly busy = input(false)
  readonly action = output<RideAction>()

  protected readonly seatLabel = SEAT_LABEL
  /** The driver's view of a ride's status (the panel's labels are for dispatchers). */
  protected readonly statusText: Record<BookingStatus, string> = {
    AwaitingPayment: $localize`Awaiting payment`,
    New: $localize`:ride status for the driver:Scheduled`,
    Assigned: $localize`:ride status for the driver:Scheduled`,
    PickedUp: $localize`On board`,
    Completed: $localize`Completed`,
    Cancelled: $localize`Cancelled`,
  }

  /** Where the car stops: the meeting point when the address has no car access. */
  protected readonly pickupStop = computed(
    () => this.ride().pickupMeetingPoint ?? this.ride().pickup
  )
  protected readonly dropoffStop = computed(
    () => this.ride().dropoffMeetingPoint ?? this.ride().dropoff
  )

  protected readonly markers = computed<MapMarker[]>(() => {
    const r = this.ride()
    const markers: MapMarker[] = [
      {
        position: [this.pickupStop().latitude, this.pickupStop().longitude],
        kind: 'pickup',
        label: $localize`Pickup`,
      },
      {
        position: [this.dropoffStop().latitude, this.dropoffStop().longitude],
        kind: 'dropoff',
        label: $localize`Drop-off`,
      },
    ]
    if (r.pickupMeetingPoint) {
      markers.push({
        position: [r.pickup.latitude, r.pickup.longitude],
        kind: 'meeting',
        label: $localize`Client's address`,
      })
    }
    if (r.dropoffMeetingPoint) {
      markers.push({
        position: [r.dropoff.latitude, r.dropoff.longitude],
        kind: 'meeting',
        label: $localize`Client's destination`,
      })
    }
    return markers
  })

  protected readonly fit = computed<LatLng[]>(() =>
    this.markers().map(m => m.position)
  )

  protected readonly whatsApp = computed(
    () => `https://wa.me/${this.ride().clientPhone.replace(/\D/g, '')}`
  )

  protected navigateTo(point: { latitude: number; longitude: number }): string {
    return `https://www.google.com/maps/dir/?api=1&destination=${point.latitude},${point.longitude}`
  }
}
