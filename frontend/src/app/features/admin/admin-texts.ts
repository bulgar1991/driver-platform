import {
  BookingStatus,
  DriverApplicationStatus,
  StaffRole,
  VehicleCategory,
} from '@/core/models'
import { BOOKING_STATUS_LABEL } from '@/core/utils/status-labels'
import { CUSTOM_CLASS_NAMES } from '@/core/services/vehicle-class.service'

/** Texts many admin pages share (form errors, names of codes), in the language of the build. */
export const ADMIN_TEXT = {
  required: $localize`Required`,
  validEmail: $localize`Enter a valid email`,
  /** A class's name: set in Admin → Pricing, built in, or its code for a class the panel doesn't know yet. */
  categories: new Proxy(
    {
      Sedan: $localize`Standard`,
      Business: $localize`Business`,
      Estate: $localize`VIP`,
      Van: $localize`Bus`,
    } as Record<VehicleCategory, string>,
    {
      get: (builtIn, code: string) =>
        CUSTOM_CLASS_NAMES[code] ?? builtIn[code] ?? code,
    }
  ),
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
