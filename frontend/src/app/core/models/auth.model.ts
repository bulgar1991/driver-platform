export type StaffRole = 'Admin' | 'Dispatcher'

export interface LoginRequest {
  email: string
  password: string
}

export interface CurrentUser {
  id: string
  email: string
  fullName: string
  role: StaffRole
  /** True while the user has a temporary password: only the password page opens. */
  mustChangePassword?: boolean
  twoFactorEnabled?: boolean
  /** Two-step sign-in is required and not set up yet: only My account opens. */
  twoFactorSetupRequired?: boolean
  /** The panel menu in this user's order (page paths); empty: the built-in order. */
  menuOrder?: string[] | null
}

export interface ChangePasswordRequest {
  currentPassword: string
  newPassword: string
}

export interface LoginResponse {
  accessToken: string
  expiresAt: string
  user: CurrentUser
}

/** The password was right; the code from the authenticator app (or email) comes next. */
export interface TwoFactorChallenge {
  twoFactorRequired: true
  challengeToken: string
  expiresAt: string
}

export interface TwoFactorStatus {
  enabled: boolean
  enabledAt: string | null
  recoveryCodesLeft: number
}

export interface TwoFactorSetup {
  /** The key to type into the app by hand, in groups of four. */
  secret: string
  setupUri: string
  /** PNG data URI of the QR code. */
  qrCode: string
}

export interface TwoFactorEnabled {
  /** Shown once. */
  recoveryCodes: string[]
  session: LoginResponse
}
