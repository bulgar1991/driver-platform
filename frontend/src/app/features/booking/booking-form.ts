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
import { todayInItaly, toLocalDateTime } from '@/core/utils/italy-time'

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

/** The return date and time are required only when a return trip is asked for. */
function returnLegValidator(group: AbstractControl): ValidationErrors | null {
  const { returnTrip, returnDate, returnTime } = (
    group as TripForm
  ).getRawValue()
  return returnTrip && (!returnDate || !returnTime)
    ? { returnRequired: true }
    : null
}

export function createBookingForms(fb: FormBuilder): BookingForms {
  const nn = fb.nonNullable
  return {
    trip: fb.group(
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
      { validators: returnLegValidator }
    ),
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
