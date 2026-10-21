import { ZoneType } from '@/core/models'

export const ZONE_TYPES: { value: ZoneType; label: string; hint: string }[] = [
  {
    value: 'NoCarAccess',
    label: 'No car access',
    hint: 'e.g. Venice. Pickups and drop-offs move to a meeting point.',
  },
  {
    value: 'LimitedTraffic',
    label: 'Limited traffic (ZTL)',
    hint: 'The access fee is added once when the ride starts or ends inside.',
  },
  {
    value: 'Airport',
    label: 'Airport',
    hint: 'Pickups use the meeting point and show its instructions.',
  },
  {
    value: 'TrainStation',
    label: 'Train station',
    hint: 'Pickups use the meeting point and show its instructions.',
  },
  {
    value: 'Port',
    label: 'Port',
    hint: 'Pickups use the meeting point and show its instructions.',
  },
]

export const ZONE_TYPE_LABEL = Object.fromEntries(
  ZONE_TYPES.map(t => [t.value, t.label])
) as Record<ZoneType, string>
