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
