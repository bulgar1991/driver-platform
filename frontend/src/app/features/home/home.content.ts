import {
  Feature,
  PartnerLogo,
  ServiceOffering,
  Stat,
  VehicleCategory,
} from '@/core/models'

/**
 * Texts of the home page. Edit here; the components only lay them out.
 * Everything below is factual for the launch (business plan): keep it that way.
 */

export const TRUST_POINTS: Feature[] = [
  {
    icon: 'tag',
    title: 'Fixed price',
    text: 'Known before you pay, never changed after',
  },
  {
    icon: 'plane',
    title: 'Flight tracked',
    text: 'Your driver adjusts to delays',
  },
  {
    icon: 'shield',
    title: 'Licensed drivers',
    text: 'NCC licence and insurance checked',
  },
  {
    icon: 'wallet',
    title: 'Card or PayPal',
    text: 'Secure payment, invoice by email',
  },
]

export const WELCOME = {
  eyebrow: 'Welcome',
  title: 'Arrive like you belong here',
  paragraphs: [
    'Magnus Drive connects airports, stations and cruise ports with hotels and homes across Italy, in a private car with a professional driver.',
    'We plan around Italy: where cars cannot go, like the historic centre of Venice, we meet you at the nearest point a car can reach and tell you exactly how to get there.',
  ],
}

/** Launch figures from the business plan (§2, §4). */
export const STATS: Stat[] = [
  { value: 5, label: 'Launch cities' },
  { value: 3, label: 'Vehicle classes' },
  { value: 8, label: 'Passengers per van' },
  { value: 100, suffix: '%', label: 'Fixed prices' },
]

export const SERVICES: ServiceOffering[] = [
  {
    title: 'Airport Transfers',
    description: 'Meet & greet in arrivals, flight tracked, fixed price.',
    icon: 'plane',
  },
  {
    title: 'Business Travel',
    description: 'Discreet chauffeurs and monthly invoicing for companies.',
    icon: 'briefcase',
  },
  {
    title: 'Stations & Ports',
    description: 'Train stations and cruise terminals across Italy.',
    icon: 'pin',
  },
  {
    title: 'Premium Service',
    description: 'Child seats, name sign and luggage space on request.',
    icon: 'diamond',
  },
]

export const VEHICLE_TEXT: Partial<
  Record<VehicleCategory, { title: string; models: string; bestFor: string }>
> = {
  Sedan: {
    title: 'Sedan',
    models: 'Mercedes E-Class or similar',
    bestFor: 'Couples and business travellers',
  },
  Estate: {
    title: 'Estate / SUV',
    models: 'E-Class Estate or similar',
    bestFor: 'Families and extra luggage',
  },
  Van: {
    title: 'Van',
    models: 'Mercedes V-Class or similar',
    bestFor: 'Groups up to 8',
  },
}

export const DRIVER_BENEFITS: Feature[] = [
  {
    icon: 'calendar',
    title: 'Rides sent to your phone',
    text: 'Every shift on your personal link, with route, passengers and flight.',
  },
  {
    icon: 'wallet',
    title: 'Clear earnings',
    text: 'Paid bookings only, commission agreed upfront.',
  },
  {
    icon: 'route',
    title: 'Choose your area',
    text: 'Tell us where you prefer to drive and when you are free.',
  },
  {
    icon: 'star',
    title: 'Grow with reviews',
    text: 'Clients rate every ride; good drivers get more of them.',
  },
]

export const LANGUAGES = [
  { code: 'it', label: 'Italiano' },
  { code: 'en', label: 'English' },
  { code: 'fr', label: 'Français' },
  { code: 'de', label: 'Deutsch' },
  { code: 'es', label: 'Español' },
  { code: 'ro', label: 'Română' },
  { code: 'ru', label: 'Русский' },
]

/** Kinds of partners the service is built for, shown until real logos are added below. */
export const PARTNER_SEGMENTS: Feature[] = [
  {
    icon: 'briefcase',
    title: 'Hotels',
    text: 'Guest transfers from the front desk',
  },
  {
    icon: 'globe',
    title: 'Travel agencies',
    text: 'Bookings through our partner API',
  },
  {
    icon: 'pin',
    title: 'Cruise lines',
    text: 'Port to airport for small groups',
  },
  {
    icon: 'calendar',
    title: 'Events & weddings',
    text: 'Several cars, one plan',
  },
  { icon: 'users', title: 'Companies', text: 'Monthly invoicing' },
  { icon: 'sparkle', title: 'Concierges', text: 'Premium cars on request' },
]

/** Logos shown only with each partner's written permission. Put the files in src/assets/partners/. */
export const PARTNER_LOGOS: PartnerLogo[] = []

/** Shown in the footer only when filled in. */
export const CONTACT = {
  email: '',
  phone: '',
  whatsapp: '',
  address: '',
}

export const SOCIAL: { label: string; url: string }[] = []
