import { StaffRole } from './auth.model'

export interface StaffMember {
  id: string
  email: string
  fullName: string
  role: StaffRole
  isActive: boolean
  mustChangePassword: boolean
  lastLoginAt: string | null
  lockedUntil: string | null
  createdAt: string
}

export interface TemporaryPassword {
  user: StaffMember
  /** Shown once. */
  temporaryPassword: string
}

export interface AuditEntry {
  id: number
  createdAt: string
  actor: string
  action: string
  entityType: string
  entityId: string | null
  changes: string | null
  ipAddress: string | null
}

export interface AuditPage {
  items: AuditEntry[]
  nextBefore: number | null
}
