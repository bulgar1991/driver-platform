import { Translations } from './geo.model'
import { VehicleCategory, VehicleClassNames } from './vehicle.model'

export interface VehicleRate {
  category: VehicleCategory
  baseFee: number
  pricePerKm: number
  maxPassengers: number
  maxLuggage: number
  names: VehicleClassNames
  isBuiltIn: boolean
  /** Hidden tariffs are not offered for new bookings. */
  isActive: boolean
  sortOrder: number
  /** Used by bookings or cars: deleting hides it instead. */
  inUse: boolean
}

export interface VehicleRateRequest {
  baseFee: number
  pricePerKm: number
  maxPassengers: number
  maxLuggage: number
  names?: VehicleClassNames | null
}

export interface DeleteVehicleRateResult {
  deleted: boolean
  hidden: boolean
  rates: VehicleRate[]
}

export type SurchargeKind = 'DaysOfWeek' | 'DateRange' | 'TimeOfDay'

export type DayOfWeek =
  | 'Sunday'
  | 'Monday'
  | 'Tuesday'
  | 'Wednesday'
  | 'Thursday'
  | 'Friday'
  | 'Saturday'

export interface SurchargeRuleRequest {
  /** In English; clients see {@link nameTranslations} in their language. */
  name: string
  nameTranslations: Translations | null
  kind: SurchargeKind
  percent: number
  daysOfWeek: DayOfWeek[]
  /** yyyy-MM-dd */
  startDate: string | null
  endDate: string | null
  /** HH:mm:ss */
  startTime: string | null
  endTime: string | null
  repeatsYearly: boolean
  isActive: boolean
}

export interface SurchargeRule extends SurchargeRuleRequest {
  id: string
}

export type ExtraType = 'ChildSeat' | 'ExtraLuggage' | 'NameSign'

export interface ExtraFee {
  type: ExtraType
  amount: number
}

export interface PricingSettings {
  vatRatePercent: number
  returnDiscountPercent: number
  minLeadTimeHours: number
}

export interface PricingOverview {
  rates: VehicleRate[]
  surcharges: SurchargeRule[]
  extras: ExtraFee[]
  settings: PricingSettings
}
