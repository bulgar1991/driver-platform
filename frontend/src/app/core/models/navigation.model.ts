import { IconName } from './icon.model'
import { StaffRole } from './auth.model'

export interface NavItem {
  label: string
  path: string
  icon: IconName
  /** Roles that see this item; all staff when omitted. */
  roles?: StaffRole[]
}
