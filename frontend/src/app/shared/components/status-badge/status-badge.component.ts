import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
} from '@angular/core'
import { AssignmentStatus, BookingStatus } from '@/core/models'
import {
  BOOKING_STATUS_LABEL,
  BOOKING_STATUS_TONE,
  SHIFT_STATUS_LABEL,
  SHIFT_STATUS_TONE,
} from '@/core/utils/status-labels'

/** Coloured label for a booking or shift status. */
@Component({
  selector: 'app-status-badge',
  template: '{{ label() }}',
  styleUrl: './status-badge.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class]': "'status-badge status-badge--' + tone()" },
})
export class StatusBadgeComponent {
  readonly kind = input<'booking' | 'shift'>('booking')
  readonly status = input.required<BookingStatus | AssignmentStatus>()

  protected readonly label = computed(() =>
    this.kind() === 'booking'
      ? BOOKING_STATUS_LABEL[this.status() as BookingStatus]
      : SHIFT_STATUS_LABEL[this.status() as AssignmentStatus]
  )

  protected readonly tone = computed(() =>
    this.kind() === 'booking'
      ? BOOKING_STATUS_TONE[this.status() as BookingStatus]
      : SHIFT_STATUS_TONE[this.status() as AssignmentStatus]
  )
}
