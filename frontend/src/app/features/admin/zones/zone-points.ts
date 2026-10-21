import { LatLng, Zone } from '@/core/models'

/** Points that frame a zone on the map: its shape and its meeting points. */
export function zonePoints(zone: Zone): LatLng[] {
  const points: LatLng[] = zone.meetingPoints.map(m => [
    m.latitude,
    m.longitude,
  ])
  if (zone.shape === 'Polygon') {
    points.push(...zone.polygon.map(p => [p.latitude, p.longitude] as LatLng))
  } else if (
    zone.centerLatitude !== null &&
    zone.centerLongitude !== null &&
    zone.radiusMeters
  ) {
    // Roughly the circle's bounding box.
    const dLat = zone.radiusMeters / 111_000
    const dLng = dLat / Math.cos((zone.centerLatitude * Math.PI) / 180)
    points.push(
      [zone.centerLatitude - dLat, zone.centerLongitude - dLng],
      [zone.centerLatitude + dLat, zone.centerLongitude + dLng]
    )
  }
  return points
}
