import { IMAGES } from '@/core/images.generated'
import { ImageAsset, VehicleCategory } from '@/core/models'

export interface VehicleInfo {
  title: string
  description: string
  /** Photo of the car on a transparent 320:150 canvas. */
  image: ImageAsset
}

/** The four tariffs: Standard, Business, VIP and Bus. */
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
