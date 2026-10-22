import {
  BookingStatus,
  DriverApplicationStatus,
  StaffRole,
  VehicleCategory,
} from '@/core/models'
import { BOOKING_STATUS_LABEL } from '@/core/utils/status-labels'

/** Texts many admin pages share (form errors, names of codes), in the language of the build. */
export const ADMIN_TEXT = {
  required: $localize`Required`,
  validEmail: $localize`Enter a valid email`,
  categories: {
    Sedan: $localize`Sedan`,
    Estate: $localize`Estate / SUV`,
    Van: $localize`Van`,
  } as Record<VehicleCategory, string>,
  bookingStatus: BOOKING_STATUS_LABEL as Record<BookingStatus, string>,
  roles: {
    Admin: $localize`Admin`,
    Dispatcher: $localize`Dispatcher`,
  } as Record<StaffRole, string>,
  applicationStatus: {
    New: $localize`New`,
    Contacted: $localize`Contacted`,
    Accepted: $localize`Accepted`,
    Rejected: $localize`Rejected`,
  } as Record<DriverApplicationStatus, string>,
} as const
