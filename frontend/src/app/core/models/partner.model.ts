export type PartnerKind = 'Company' | 'Booking'

export interface PartnerRequest {
  name: string
  kind: PartnerKind
  contactEmail: string
  /** Agreed rate on top of the public price, e.g. -10. */
  priceAdjustmentPercent: number
  commissionPercent: number
  monthlyInvoicing: boolean
  notifyPassengers: boolean
  webhookUrl: string | null
  isActive: boolean
}

export interface Partner extends PartnerRequest {
  id: string
  apiKeyPrefix: string | null
  apiKeyCreatedAt: string | null
  apiKeyLastUsedAt: string | null
  bookingsThisMonth: number
  webhooksFailing: number
}

export interface PartnerApiKey {
  /** Shown once. */
  apiKey: string
  prefix: string
}

export interface WebhookDelivery {
  id: number
  event: string
  bookingReference: string
  attempts: number
  createdAt: string
  deliveredAt: string | null
  lastError: string | null
}
