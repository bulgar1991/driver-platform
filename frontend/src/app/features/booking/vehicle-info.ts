import { VehicleCategory } from '@/core/models'

export interface VehicleInfo {
  title: string
  description: string
}

/** Business plan §4: berlină, break / SUV, van. */
export const VEHICLE_INFO: Record<VehicleCategory, VehicleInfo> = {
  Sedan: {
    title: $localize`Sedan`,
    description: $localize`Business sedan, e.g. Mercedes E-Class or similar`,
  },
  Estate: {
    title: $localize`Estate / SUV`,
    description: $localize`More room for luggage, e.g. Mercedes E-Class Estate`,
  },
  Van: {
    title: $localize`Van`,
    description: $localize`Groups and families, e.g. Mercedes V-Class`,
  },
}
