import { DriverDocumentType } from './driver.model'

/** Where a deadline comes from: a driver's document, a field of the car, or one of the car's own deadlines. */
export type DeadlineSource =
  'DriverDocument' | 'Insurance' | 'Inspection' | 'Service' | 'VehicleDeadline'

export type DeadlineState = 'Expired' | 'DueSoon' | 'Later'
export type DeadlineOwner = 'Driver' | 'Vehicle'

export interface ExpirationItem {
  source: DeadlineSource
  ownerKind: DeadlineOwner
  ownerId: string
  ownerName: string
  /** The car's make and model, or the driver's phone. */
  ownerDetail: string | null
  ownerActive: boolean
  recordId: string | null
  documentType: DriverDocumentType | null
  title: string | null
  /** Document number, or the notes of a vehicle deadline. */
  reference: string | null
  dueOn: string | null
  dueKm: number | null
  /** Negative when overdue. */
  daysLeft: number | null
  kmLeft: number | null
  state: DeadlineState
}

export interface MissingDocument {
  ownerKind: DeadlineOwner
  ownerId: string
  ownerName: string
  ownerActive: boolean
  problem: string
}

export interface ExpirationsResponse {
  items: ExpirationItem[]
  missing: MissingDocument[]
  today: string
  alertDays: number
  alertKm: number
}

export interface RenewDeadlineRequest {
  source: DeadlineSource
  ownerId: string
  recordId: string | null
  dueOn: string | null
  dueKm: number | null
}
