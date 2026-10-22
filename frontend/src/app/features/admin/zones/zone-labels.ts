import { ZoneType } from '@/core/models'

export const ZONE_TYPES: { value: ZoneType; label: string; hint: string }[] = [
  {
    value: 'NoCarAccess',
    label: $localize`No car access`,
    hint: $localize`e.g. Venice. Pickups and drop-offs move to a meeting point.`,
  },
  {
    value: 'LimitedTraffic',
    label: $localize`Limited traffic (ZTL)`,
    hint: $localize`The access fee is added once when the ride starts or ends inside.`,
  },
  {
    value: 'Airport',
    label: $localize`Airport`,
    hint: $localize`Pickups use the meeting point and show its instructions.`,
  },
  {
    value: 'TrainStation',
    label: $localize`Train station`,
    hint: $localize`Pickups use the meeting point and show its instructions.`,
  },
  {
    value: 'Port',
    label: $localize`Port`,
    hint: $localize`Pickups use the meeting point and show its instructions.`,
  },
]

export const ZONE_TYPE_LABEL = Object.fromEntries(
  ZONE_TYPES.map(t => [t.value, t.label])
) as Record<ZoneType, string>
