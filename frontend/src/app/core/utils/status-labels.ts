import { AssignmentStatus, BookingStatus } from '../models'

export const BOOKING_STATUS_LABEL: Record<BookingStatus, string> = {
  AwaitingPayment: $localize`Awaiting payment`,
  New: $localize`To assign`,
  Assigned: $localize`Assigned`,
  PickedUp: $localize`Picked up`,
  Completed: $localize`Completed`,
  Cancelled: $localize`Cancelled`,
}

export const SHIFT_STATUS_LABEL: Record<AssignmentStatus, string> = {
  Draft: $localize`Draft`,
  Sent: $localize`Sent to driver`,
  Seen: $localize`Seen by driver`,
  Confirmed: $localize`Confirmed`,
  Completed: $localize`Completed`,
  Cancelled: $localize`Cancelled`,
}

/** Colour family for a status badge. */
export type StatusTone = 'neutral' | 'warning' | 'info' | 'success' | 'danger'

export const BOOKING_STATUS_TONE: Record<BookingStatus, StatusTone> = {
  AwaitingPayment: 'neutral',
  New: 'warning',
  Assigned: 'info',
  PickedUp: 'info',
  Completed: 'success',
  Cancelled: 'danger',
}

export const SHIFT_STATUS_TONE: Record<AssignmentStatus, StatusTone> = {
  Draft: 'warning',
  Sent: 'info',
  Seen: 'info',
  Confirmed: 'success',
  Completed: 'neutral',
  Cancelled: 'danger',
}
