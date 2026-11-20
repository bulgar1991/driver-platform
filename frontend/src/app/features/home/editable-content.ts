import { IMAGES } from '@/core/images.generated'
import { ImageAsset, VehicleCategory } from '@/core/models'
import { AREA_IN } from '@/core/area'
import { VEHICLE_INFO, VEHICLE_KEY } from '@/features/booking/vehicle-info'

export { VEHICLE_KEY }
import {
  DRIVER_BENEFITS,
  PARTNER_SEGMENTS,
  SERVICES,
  STATS,
  VEHICLE_TEXT,
  WELCOME,
} from './home.content'

/**
 * What can be changed in Admin → Website content, with the site's built-in text or picture for
 * each key. The home page sections read the same keys through SiteContentService; a key nobody
 * changed shows the built-in content. Keys are stored in the database: do not rename them.
 */
export type EditableField =
  | { key: string; label: string; kind: 'text' | 'long'; builtIn: string }
  | { key: string; label: string; kind: 'image'; builtIn: ImageAsset }

export interface EditableGroup {
  title: string
  fields: EditableField[]
}

export interface EditableSection {
  id: string
  title: string
  groups: EditableGroup[]
}

/** Headings in code (they were template attributes): same wording, so the translations stay. */
export const HEADINGS = {
  servicesEyebrow: $localize`What we do`,
  servicesTitle: $localize`Our Services`,
  tariffsEyebrow: $localize`See our`,
  tariffsTitle: $localize`Tariffs`,
  driversEyebrow: $localize`For Drivers`,
  driversTitle: $localize`Do you want to`,
  driversTitleAccent: $localize`earn with us?`,
  driversLead: $localize`We work with licensed professional drivers ${AREA_IN}:area:, with or without their own car. Tell us about you; we answer within a few days.`,
  partnersEyebrow: $localize`Our partners`,
  partnersTitle: $localize`And clients`,
  partnersLead: $localize`Hotels, agencies and cruise companies book for their guests through our partner API or the panel, with agreed rates and monthly invoicing.`,
  testimonialsEyebrow: $localize`Happy Clients`,
  testimonialsTitle: $localize`Testimonials`,
  welcomeImageAlt: $localize`Black Mercedes with a chauffeur in Italy`,
}

const L = {
  eyebrow: $localize`:Website content field:Small heading above the title`,
  title: $localize`:Website content field:Title`,
  titleAccent: $localize`:Website content field:Title, second part (in gold)`,
  text: $localize`:Website content field:Text`,
  paragraph: (n: number) =>
    $localize`:Website content field:Paragraph ${n}:number:`,
  picture: $localize`:Website content field:Picture`,
  pictureAlt: $localize`:Website content field:Picture description (for screen readers and Google)`,
  value: $localize`:Website content field:Number`,
  valueLabel: $localize`:Website content field:Label under the number`,
  models: $localize`:Website content field:Car models`,
  bestFor: $localize`:Website content field:Best for`,
  description: $localize`:Website content field:Description on the booking page`,
  heading: $localize`:Website content group:Heading`,
  figure: (n: number) => $localize`:Website content group:Figure ${n}:number:`,
}

const text = (key: string, label: string, builtIn: string): EditableField => ({
  key,
  label,
  kind: builtIn.length > 90 ? 'long' : 'text',
  builtIn,
})

export const EDITABLE_SECTIONS: EditableSection[] = [
  {
    id: 'welcome',
    title: $localize`:Website content section:Welcome`,
    groups: [
      {
        title: L.heading,
        fields: [
          text('welcome.eyebrow', L.eyebrow, WELCOME.eyebrow),
          text('welcome.title', L.title, WELCOME.title),
          ...WELCOME.paragraphs.map((p, i) =>
            text(`welcome.paragraph.${i + 1}`, L.paragraph(i + 1), p)
          ),
          {
            key: 'welcome.image',
            label: L.picture,
            kind: 'image',
            builtIn: IMAGES.hero,
          },
          text('welcome.image-alt', L.pictureAlt, HEADINGS.welcomeImageAlt),
        ],
      },
      ...STATS.map((s, i) => ({
        title: L.figure(i + 1),
        fields: [
          text(`stats.${i + 1}.value`, L.value, `${s.value}`),
          text(`stats.${i + 1}.label`, L.valueLabel, s.label),
        ],
      })),
    ],
  },
  {
    id: 'services',
    title: $localize`:Website content section:Services`,
    groups: [
      {
        title: L.heading,
        fields: [
          text('services.eyebrow', L.eyebrow, HEADINGS.servicesEyebrow),
          text('services.title', L.title, HEADINGS.servicesTitle),
        ],
      },
      ...SERVICES.map((s, i) => ({
        title: s.title,
        fields: [
          text(`services.${i + 1}.title`, L.title, s.title),
          text(`services.${i + 1}.text`, L.text, s.description),
        ],
      })),
    ],
  },
  {
    id: 'vehicles',
    title: $localize`:Website content section:Cars and tariffs`,
    groups: [
      {
        title: L.heading,
        fields: [
          text('tariffs.eyebrow', L.eyebrow, HEADINGS.tariffsEyebrow),
          text('tariffs.title', L.title, HEADINGS.tariffsTitle),
        ],
      },
      ...(Object.keys(VEHICLE_KEY) as VehicleCategory[]).map(category => {
        const key = VEHICLE_KEY[category]
        const info = VEHICLE_INFO[category]
        const card = VEHICLE_TEXT[category]
        return {
          title: card?.title ?? info.title,
          fields: [
            text(`${key}.title`, L.title, card?.title ?? info.title),
            text(`${key}.models`, L.models, card?.models ?? ''),
            text(`${key}.best-for`, L.bestFor, card?.bestFor ?? ''),
            text(`${key}.description`, L.description, info.description),
            {
              key: `${key}.image`,
              label: L.picture,
              kind: 'image' as const,
              builtIn: info.image,
            },
          ],
        }
      }),
    ],
  },
  {
    id: 'drivers',
    title: $localize`:Website content section:For drivers`,
    groups: [
      {
        title: L.heading,
        fields: [
          text('drivers.eyebrow', L.eyebrow, HEADINGS.driversEyebrow),
          text('drivers.title', L.title, HEADINGS.driversTitle),
          text(
            'drivers.title-accent',
            L.titleAccent,
            HEADINGS.driversTitleAccent
          ),
          text('drivers.lead', L.text, HEADINGS.driversLead),
        ],
      },
      ...DRIVER_BENEFITS.map((b, i) => ({
        title: b.title,
        fields: [
          text(`drivers.${i + 1}.title`, L.title, b.title),
          text(`drivers.${i + 1}.text`, L.text, b.text),
        ],
      })),
    ],
  },
  {
    id: 'partners',
    title: $localize`:Website content section:Partners`,
    groups: [
      {
        title: L.heading,
        fields: [
          text('partners.eyebrow', L.eyebrow, HEADINGS.partnersEyebrow),
          text('partners.title', L.title, HEADINGS.partnersTitle),
          text('partners.lead', L.text, HEADINGS.partnersLead),
        ],
      },
      ...PARTNER_SEGMENTS.map((s, i) => ({
        title: s.title,
        fields: [
          text(`partners.${i + 1}.title`, L.title, s.title),
          text(`partners.${i + 1}.text`, L.text, s.text),
        ],
      })),
    ],
  },
  {
    id: 'testimonials',
    title: $localize`:Website content section:Reviews`,
    groups: [
      {
        title: L.heading,
        fields: [
          text('testimonials.eyebrow', L.eyebrow, HEADINGS.testimonialsEyebrow),
          text('testimonials.title', L.title, HEADINGS.testimonialsTitle),
        ],
      },
    ],
  },
]
