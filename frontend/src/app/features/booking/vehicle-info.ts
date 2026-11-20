import { IMAGES } from '@/core/images.generated'
import { ImageAsset, VehicleCategory } from '@/core/models'

export interface VehicleInfo {
  title: string
  description: string
  /** Photo of the car on a transparent 320:150 canvas. */
  image: ImageAsset
}

/** The four built-in tariffs: Standard, Business, VIP and Bus. */
export const VEHICLE_INFO: Record<VehicleCategory, VehicleInfo> = {
  Sedan: {
    title: $localize`Standard`,
    description: $localize`Comfortable sedan, e.g. Toyota Camry or similar`,
    image: IMAGES['car-standard'],
  },
  Business: {
    title: $localize`Business`,
    description: $localize`Premium car, e.g. BMW 4 Series or similar`,
    image: IMAGES['car-business'],
  },
  Estate: {
    title: $localize`VIP`,
    description: $localize`Premium SUV with room for luggage, e.g. BMW X5`,
    image: IMAGES['car-vip'],
  },
  Van: {
    title: $localize`Bus`,
    description: $localize`Groups and families, e.g. Mercedes Sprinter`,
    image: IMAGES['car-bus'],
  },
}

/** Key prefix of the built-in classes in Admin → Website content ("vehicle.bus.title"…). Stored: do not rename. */
export const VEHICLE_KEY: Record<VehicleCategory, string> = {
  Sedan: 'vehicle.standard',
  Business: 'vehicle.business',
  Estate: 'vehicle.vip',
  Van: 'vehicle.bus',
}

/** Key prefix in Admin → Website content for any class: "vehicle.minibus16" for one added in Pricing. */
export function vehicleKey(category: VehicleCategory): string {
  return VEHICLE_KEY[category] ?? `vehicle.${category.toLowerCase()}`
}

/**
 * What the site shows for a class: the built-in name, description and picture, or for a class added
 * in Admin → Pricing its name (from the API) and a generic picture until one is uploaded.
 */
export function vehicleInfo(
  category: VehicleCategory,
  name?: string | null
): VehicleInfo {
  const builtIn = VEHICLE_INFO[category]
  if (builtIn) {
    return name ? { ...builtIn, title: name } : builtIn
  }
  return {
    title: name || category,
    description: '',
    image: IMAGES['car-standard'],
  }
}
