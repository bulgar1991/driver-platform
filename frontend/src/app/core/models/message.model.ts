import {
  NotificationChannel,
  NotificationKind,
  NotificationStatus,
} from './driver-portal.model'

export type MessageAudience = 'Any' | 'Driver' | 'Client' | 'Partner'

export interface MessageQuery {
  /** Office dates the message was sent (yyyy-MM-dd), inclusive. */
  from: string
  to: string
  search: string
  channel: NotificationChannel | null
  status: NotificationStatus | null
  kind: NotificationKind | null
  audience: MessageAudience
  page: number
  pageSize: number
}

export interface MessageItem {
  id: number
  /** Office time. */
  sentLocal: string
  kind: NotificationKind
  channel: NotificationChannel
  status: NotificationStatus
  recipient: string
  subject: string | null
  error: string | null
  bookingId: string | null
  bookingReference: string | null
  clientName: string | null
  clientId: string | null
  driverId: string | null
  driverName: string | null
  assignmentId: string | null
  shiftDate: string | null
  canResend: boolean
}

export interface MessageCounts {
  total: number
  sent: number
  failed: number
  skipped: number
  email: number
  whatsApp: number
  sms: number
}

export interface MessageList {
  items: MessageItem[]
  total: number
  page: number
  pageSize: number
  counts: MessageCounts
}
