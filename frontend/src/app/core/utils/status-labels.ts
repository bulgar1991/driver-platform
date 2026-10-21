import { AssignmentStatus, BookingStatus } from '../models'

export const BOOKING_STATUS_LABEL: Record<BookingStatus, string> = {
  AwaitingPayment: 'Awaiting payment',
  New: 'To assign',
  Assigned: 'Assigned',
  PickedUp: 'Picked up',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
}

export const SHIFT_STATUS_LABEL: Record<AssignmentStatus, string> = {
  Draft: 'Draft',
  Sent: 'Sent to driver',
  Seen: 'Seen by driver',
  Confirmed: 'Confirmed',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
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
