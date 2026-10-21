import { BookingLegSummary, BookingStatus } from './booking.model'
import { VehicleCategory } from './vehicle.model'

export type PaymentMethod = 'Card' | 'PayPal'

export type PaymentStatus =
  'Pending' | 'Succeeded' | 'Failed' | 'Refunded' | 'PartiallyRefunded'

export type InvoiceType = 'Invoice' | 'CreditNote'

export type InvoiceStatus = 'Issued' | 'Sent' | 'Cancelled'

/** The client's own booking page, opened from the signed link. */
export interface MyBooking {
  reference: string
  status: BookingStatus
  vehicleCategory: VehicleCategory
  clientName: string
  legs: BookingLegSummary[]
  total: number
  vatRatePercent: number
  currency: string
  canPay: boolean
  payment: {
    status: PaymentStatus
    method: PaymentMethod | null
    amount: number
    refundedAmount: number
    paidAt: string | null
  } | null
  invoices: {
    number: string
    type: InvoiceType
    totalAmount: number
    issuedAt: string
  }[]
}

export interface InvoiceSummary {
  id: string
  number: string
  type: InvoiceType
  totalAmount: number
  issuedAt: string
  status: InvoiceStatus
}

export interface PaymentListItem {
  id: string
  bookingReference: string
  bookingStatus: BookingStatus
  clientName: string
  clientEmail: string | null
  amount: number
  refundedAmount: number
  currency: string
  status: PaymentStatus
  method: PaymentMethod | null
  provider: string
  createdAt: string
  paidAt: string | null
  invoices: InvoiceSummary[]
}

export interface RefundRequest {
  /** Omit to refund everything that is left. */
  amount?: number | null
  reason: string
  cancelBooking: boolean
}

/** The development payment page (no Stripe keys). */
export interface DevPaymentSession {
  reference: string
  amount: number
  currency: string
  lines: { name: string; description: string | null; amount: number }[]
}
