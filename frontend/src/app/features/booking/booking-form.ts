import {
  AbstractControl,
  FormBuilder,
  FormControl,
  FormGroup,
  ValidationErrors,
  Validators,
} from '@angular/forms'
import {
  ChildSeatGroup,
  ChildSeatRequest,
  CreateBookingRequest,
  GeoPoint,
  QuoteRequest,
  VehicleCategory,
} from '@/core/models'
import { Params } from '@angular/router'
import {
  nowInItaly,
  todayInItaly,
  toLocalDateTime,
} from '@/core/utils/italy-time'

export type TripForm = FormGroup<{
  pickup: FormControl<GeoPoint | null>
  dropoff: FormControl<GeoPoint | null>
  date: FormControl<string>
  time: FormControl<string>
  passengers: FormControl<number>
  largeLuggage: FormControl<number>
  smallLuggage: FormControl<number>
  returnTrip: FormControl<boolean>
  returnDate: FormControl<string>
  returnTime: FormControl<string>
  pickupMeetingPoint: FormControl<string | null>
  dropoffMeetingPoint: FormControl<string | null>
}>

export type DetailsForm = FormGroup<{
  infantSeats: FormControl<number>
  toddlerSeats: FormControl<number>
  boosterSeats: FormControl<number>
  nameSign: FormControl<boolean>
  nameSignText: FormControl<string>
  flightNumber: FormControl<string>
  returnNameSign: FormControl<boolean>
  returnFlightNumber: FormControl<string>
  notes: FormControl<string>
}>

export type ContactForm = FormGroup<{
  firstName: FormControl<string>
  lastName: FormControl<string>
  email: FormControl<string>
  phone: FormControl<string>
  gdprConsent: FormControl<boolean>
  marketingConsent: FormControl<boolean>
}>

export interface BookingForms {
  trip: TripForm
  details: DetailsForm
  contact: ContactForm
}

const FLIGHT_PATTERN = /^[A-Za-z0-9 ]{2,16}$/

/** The API's own default; the booking options bring the real value. */
export const DEFAULT_MIN_LEAD_HOURS = 2

/** The return date and time are required only when a return trip is asked for. */
function returnLegValidator(group: AbstractControl): ValidationErrors | null {
  const { returnTrip, returnDate, returnTime, date, time } = (
    group as TripForm
  ).getRawValue()
  if (!returnTrip) {
    return null
  }
  if (!returnDate || !returnTime) {
    return { returnRequired: true }
  }
  return toLocalDateTime(returnDate, returnTime) <= toLocalDateTime(date, time)
    ? { returnBeforeOutbound: true }
    : null
}

/**
 * The API refuses pickups sooner than its minimum notice; checking here first shows the reason
 * next to the date at once, without a refused request.
 */
function leadTimeValidator(minLeadHours: () => number) {
  return (group: AbstractControl): ValidationErrors | null => {
    const { date, time } = (group as TripForm).getRawValue()
    if (!date || !time) {
      return null
    }
    const hours = minLeadHours()
    const pickup = toLocalDateTime(date, time).slice(0, 16)
    return pickup < nowInItaly(hours * 60) ? { tooSoon: { hours } } : null
  }
}

/** From, to, when and how many: the search bar on the home page and on the booking page. */
export function createTripForm(
  fb: FormBuilder,
  minLeadHours: () => number = () => DEFAULT_MIN_LEAD_HOURS
): TripForm {
  const nn = fb.nonNullable
  return fb.group(
    {
      pickup: fb.control<GeoPoint | null>(null, Validators.required),
      dropoff: fb.control<GeoPoint | null>(null, Validators.required),
      date: nn.control(todayInItaly(1), Validators.required),
      time: nn.control('10:00', Validators.required),
      passengers: nn.control(2, [Validators.min(1), Validators.max(8)]),
      largeLuggage: nn.control(2),
      smallLuggage: nn.control(0),
      returnTrip: nn.control(false),
      returnDate: nn.control(''),
      returnTime: nn.control(''),
      pickupMeetingPoint: fb.control<string | null>(null),
      dropoffMeetingPoint: fb.control<string | null>(null),
    },
    { validators: [returnLegValidator, leadTimeValidator(minLeadHours)] }
  )
}

export function createBookingForms(
  fb: FormBuilder,
  minLeadHours?: () => number
): BookingForms {
  const nn = fb.nonNullable
  return {
    trip: createTripForm(fb, minLeadHours),
    details: nn.group({
      infantSeats: nn.control(0),
      toddlerSeats: nn.control(0),
      boosterSeats: nn.control(0),
      nameSign: nn.control(false),
      nameSignText: nn.control('', Validators.maxLength(60)),
      flightNumber: nn.control('', Validators.pattern(FLIGHT_PATTERN)),
      returnNameSign: nn.control(false),
      returnFlightNumber: nn.control('', Validators.pattern(FLIGHT_PATTERN)),
      notes: nn.control('', Validators.maxLength(2000)),
    }),
    contact: nn.group({
      firstName: nn.control('', [
        Validators.required,
        Validators.maxLength(100),
      ]),
      lastName: nn.control('', [
        Validators.required,
        Validators.maxLength(100),
      ]),
      email: nn.control('', [Validators.required, Validators.email]),
      phone: nn.control('', [
        Validators.required,
        Validators.pattern(/^\+?[0-9 ()-]{6,20}$/),
      ]),
      gdprConsent: nn.control(false, Validators.requiredTrue),
      marketingConsent: nn.control(false),
    }),
  }
}

function childSeats(details: DetailsForm): ChildSeatRequest[] {
  const v = details.getRawValue()
  const seats: [ChildSeatGroup, number][] = [
    ['Infant', v.infantSeats],
    ['Toddler', v.toddlerSeats],
    ['Booster', v.boosterSeats],
  ]
  return seats
    .filter(([, count]) => count > 0)
    .map(([group, count]) => ({ group, count }))
}

export function toQuoteRequest(forms: BookingForms): QuoteRequest {
  const trip = forms.trip.getRawValue()
  const details = forms.details.getRawValue()
  return {
    pickup: trip.pickup!,
    dropoff: trip.dropoff!,
    pickupAt: toLocalDateTime(trip.date, trip.time),
    passengers: trip.passengers,
    largeLuggage: trip.largeLuggage,
    smallLuggage: trip.smallLuggage,
    childSeats: childSeats(forms.details),
    nameSign: details.nameSign,
    pickupMeetingPoint: trip.pickupMeetingPoint,
    dropoffMeetingPoint: trip.dropoffMeetingPoint,
    return: trip.returnTrip
      ? {
          pickupAt: toLocalDateTime(trip.returnDate, trip.returnTime),
          nameSign: details.returnNameSign,
          flightNumber: details.returnFlightNumber.trim() || null,
        }
      : null,
  }
}

export function toBookingRequest(
  forms: BookingForms,
  vehicleCategory: VehicleCategory,
  expectedTotal: number | null,
  language: string
): CreateBookingRequest {
  const details = forms.details.getRawValue()
  const contact = forms.contact.getRawValue()
  return {
    trip: toQuoteRequest(forms),
    vehicleCategory,
    expectedTotal,
    contact: {
      firstName: contact.firstName.trim(),
      lastName: contact.lastName.trim(),
      email: contact.email.trim(),
      phone: contact.phone.trim(),
      language,
    },
    flightNumber: details.flightNumber.trim() || null,
    nameSignText: details.nameSign ? details.nameSignText.trim() || null : null,
    notes: details.notes.trim() || null,
    gdprConsent: contact.gdprConsent,
    marketingConsent: contact.marketingConsent,
  }
}

/** The search as link parameters: the home page's search bar opens the booking page with them. */
export function tripToParams(trip: TripForm): Params {
  const v = trip.getRawValue()
  const point = (p: GeoPoint | null) =>
    p ? [p.address, p.latitude, p.longitude] : [null, null, null]
  const [from, fromLat, fromLng] = point(v.pickup)
  const [to, toLat, toLng] = point(v.dropoff)
  return {
    from,
    fromLat,
    fromLng,
    to,
    toLat,
    toLng,
    date: v.date,
    time: v.time,
    pax: v.passengers,
    bags: v.largeLuggage,
    small: v.smallLuggage,
    returnDate: v.returnTrip ? v.returnDate : null,
    returnTime: v.returnTrip ? v.returnTime : null,
  }
}

/** The values a link from tripToParams carries; whatever is missing or malformed is left out. */
export function tripFromParams(
  params: Params
): Partial<ReturnType<TripForm['getRawValue']>> {
  // From the address bar every value is text; from tripToParams directly, numbers too.
  const text = (key: string): string | null => {
    const value: unknown = params[key]
    if (typeof value === 'number') {
      return String(value)
    }
    return typeof value === 'string' && value ? value : null
  }
  const number = (key: string): number | null => {
    const value = Number(text(key))
    return text(key) !== null && Number.isFinite(value) ? value : null
  }
  const point = (prefix: 'from' | 'to'): GeoPoint | null => {
    const address = text(prefix)
    const latitude = number(`${prefix}Lat`)
    const longitude = number(`${prefix}Lng`)
    return address && latitude !== null && longitude !== null
      ? { address, latitude, longitude }
      : null
  }
  const count = (key: string, min: number, max: number) => {
    const value = number(key)
    return value !== null &&
      Number.isInteger(value) &&
      value >= min &&
      value <= max
      ? value
      : null
  }
  const date = (key: string) =>
    /^\d{4}-\d{2}-\d{2}$/.test(text(key) ?? '') ? text(key)! : null
  const time = (key: string) =>
    /^\d{2}:\d{2}$/.test(text(key) ?? '') ? text(key)! : null

  const values = {
    pickup: point('from'),
    dropoff: point('to'),
    date: date('date'),
    time: time('time'),
    passengers: count('pax', 1, 8),
    largeLuggage: count('bags', 0, 16),
    smallLuggage: count('small', 0, 16),
    returnDate: date('returnDate'),
    returnTime: time('returnTime'),
  }
  const found = Object.fromEntries(
    Object.entries(values).filter(([, value]) => value !== null)
  )
  return values.returnDate && values.returnTime
    ? { ...found, returnTrip: true }
    : found
}
