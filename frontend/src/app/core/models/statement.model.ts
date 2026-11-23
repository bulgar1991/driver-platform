import { BookingStatus } from './booking.model'
import { PartnerKind } from './partner.model'

export type StatementStatus =
  'NotIssued' | 'Issued' | 'Sent' | 'Overdue' | 'Paid'

export interface StatementRow {
  partnerId: string
  partnerName: string
  partnerKind: PartnerKind
  contactEmail: string
  monthlyInvoicing: boolean
  status: StatementStatus
  number: string | null
  rides: number
  cancelled: number
  revenue: number
  commissionPercent: number
  commission: number
  net: number
  issuedAt: string | null
  dueOn: string | null
  sentAt: string | null
  sentTo: string | null
  paidAt: string | null
  note: string | null
  /** Issued, but the rides changed since: issue it again. */
  changed: boolean
}

export interface StatementTotals {
  partners: number
  notIssued: number
  toIssue: number
  billed: number
  outstanding: number
  paid: number
  commission: number
  overdue: number
}

export interface StatementMonth {
  year: number
  month: number
  monthOver: boolean
  items: StatementRow[]
  totals: StatementTotals
  currency: string
}

export interface StatementLine {
  bookingId: string
  reference: string
  externalReference: string | null
  /** Local time at the pickup. */
  pickupLocal: string
  passenger: string
  from: string
  to: string
  vehicleCategory: string
  status: BookingStatus
  price: number
  commission: number
}

export interface StatementDetail {
  row: StatementRow
  lines: StatementLine[]
  currency: string
}
