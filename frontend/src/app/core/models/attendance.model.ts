import { DriverContractType } from './driver.model'

/** A kind of event (holiday, sick leave…), with its colour in the calendar. */
export interface AttendanceType {
  id: string
  name: string
  /** One to three letters shown in the cell. */
  code: string
  /** "#22c55e" */
  color: string
  /** A whole day of it takes the person off the available count and out of shifts. */
  makesUnavailable: boolean
  sortOrder: number
}

export type AttendanceTypeRequest = Omit<AttendanceType, 'id'>

export interface AttendancePerson {
  id: string
  firstName: string
  lastName: string
  employeeNumber: string | null
  contractType: DriverContractType
  jobFunction: string | null
  isActive: boolean
  /** yyyy-MM-dd */
  hiredOn: string | null
  leftOn: string | null
}

export interface AttendanceEntry {
  id: string
  driverId: string
  typeId: string
  /** yyyy-MM-dd */
  date: string
  /** HH:mm:ss; both null for a whole day. */
  startTime: string | null
  endTime: string | null
  notes: string | null
}

export interface AttendanceCalendar {
  types: AttendanceType[]
  people: AttendancePerson[]
  entries: AttendanceEntry[]
}

/** Puts the event on every day from `from` to `to`, replacing what was there. */
export interface AttendanceRequest {
  driverId: string
  typeId: string
  from: string
  to: string
  startTime?: string | null
  endTime?: string | null
  skipWeekends: boolean
  notes?: string | null
}
