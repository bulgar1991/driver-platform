import {
  Feature,
  PartnerLogo,
  ServiceOffering,
  Stat,
  VehicleCategory,
} from '@/core/models'

/**
 * Texts of the home page. Edit here; the components only lay them out. Every visible text is
 * marked with $localize: after editing, run `npm run i18n` and translate the new messages.
 * Everything below is factual for the launch (business plan): keep it that way.
 */

export const WELCOME = {
  eyebrow: $localize`Welcome`,
  title: $localize`Arrive like you belong here`,
  paragraphs: [
    $localize`Magnus Drive connects airports, stations and cruise ports with hotels and homes across Italy and over the border to France, Switzerland and Germany, in a private car with a professional driver.`,
    $localize`We plan around Italy: where cars cannot go, like the historic centre of Venice, we meet you at the nearest point a car can reach and tell you exactly how to get there.`,
  ],
}

/** Launch figures from the business plan (§2, §4). */
export const STATS: Stat[] = [
  { value: 5, label: $localize`Launch cities` },
  { value: 3, label: $localize`Vehicle classes` },
  { value: 8, label: $localize`Passengers per van` },
  { value: 100, suffix: '%', label: $localize`Fixed prices` },
]

export const SERVICES: ServiceOffering[] = [
  {
    title: $localize`Airport Transfers`,
    description: $localize`Meet & greet in arrivals, flight tracked, fixed price.`,
    icon: 'plane',
  },
  {
    title: $localize`Business Travel`,
    description: $localize`Discreet chauffeurs and monthly invoicing for companies.`,
    icon: 'briefcase',
  },
  {
    title: $localize`Stations & Ports`,
    description: $localize`Train stations and cruise terminals, in Italy and over the border.`,
    icon: 'pin',
  },
  {
    title: $localize`Premium Service`,
    description: $localize`Child seats, name sign and luggage space on request.`,
    icon: 'diamond',
  },
  {
    title: $localize`Cross-border Transfers`,
    description: $localize`Milan to Zurich, Nice to Genoa, Munich to Verona: one fixed price over the border.`,
    icon: 'globe',
  },
  {
    title: $localize`Venice Transfers`,
    description: $localize`Pickup at the nearest point cars can reach, with clear directions.`,
    icon: 'map',
  },
  {
    title: $localize`Groups & Families`,
    description: $localize`Vans for up to 8 passengers, child seats for every age.`,
    icon: 'users',
  },
  {
    title: $localize`Return Trips`,
    description: $localize`Book both ways at once and get a driver for the way back.`,
    icon: 'route',
  },
  {
    title: $localize`Hotels & Agencies`,
    description: $localize`Partner rates, our booking API and monthly invoicing.`,
    icon: 'globe',
  },
]

export const VEHICLE_TEXT: Partial<
  Record<VehicleCategory, { title: string; models: string; bestFor: string }>
> = {
  Sedan: {
    title: $localize`Sedan`,
    models: $localize`Mercedes E-Class or similar`,
    bestFor: $localize`Couples and business travellers`,
  },
  Estate: {
    title: $localize`Estate / SUV`,
    models: $localize`E-Class Estate or similar`,
    bestFor: $localize`Families and extra luggage`,
  },
  Van: {
    title: $localize`Van`,
    models: $localize`Mercedes V-Class or similar`,
    bestFor: $localize`Groups up to 8`,
  },
}

export const DRIVER_BENEFITS: Feature[] = [
  {
    icon: 'calendar',
    title: $localize`Rides sent to your phone`,
    text: $localize`Every shift on your personal link, with route, passengers and flight.`,
  },
  {
    icon: 'wallet',
    title: $localize`Clear earnings`,
    text: $localize`Paid bookings only, commission agreed upfront.`,
  },
  {
    icon: 'route',
    title: $localize`Choose your area`,
    text: $localize`Tell us where you prefer to drive and when you are free.`,
  },
  {
    icon: 'star',
    title: $localize`Grow with reviews`,
    text: $localize`Clients rate every ride; good drivers get more of them.`,
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
    title: $localize`Hotels`,
    text: $localize`Guest transfers from the front desk`,
  },
  {
    icon: 'globe',
    title: $localize`Travel agencies`,
    text: $localize`Bookings through our partner API`,
  },
  {
    icon: 'pin',
    title: $localize`Cruise lines`,
    text: $localize`Port to airport for small groups`,
  },
  {
    icon: 'calendar',
    title: $localize`Events & weddings`,
    text: $localize`Several cars, one plan`,
  },
  {
    icon: 'users',
    title: $localize`Companies`,
    text: $localize`Monthly invoicing`,
  },
  {
    icon: 'sparkle',
    title: $localize`Concierges`,
    text: $localize`Premium cars on request`,
  },
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
