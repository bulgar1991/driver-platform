import { VehicleCategory } from './vehicle.model'

export interface VehicleRate {
  category: VehicleCategory
  baseFee: number
  pricePerKm: number
  maxPassengers: number
  maxLuggage: number
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
  name: string
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
