import { Pipe, PipeTransform } from '@angular/core'

/** 95 → "1 h 35 min". */
@Pipe({ name: 'duration' })
export class DurationPipe implements PipeTransform {
  transform(minutes: number | null | undefined): string {
    if (!minutes) {
      return ''
    }
    const h = Math.floor(minutes / 60)
    const m = minutes % 60
    return h === 0
      ? $localize`${m}:minutes: min`
      : m === 0
        ? $localize`${h}:hours: h`
        : $localize`${h}:hours: h ${m}:minutes: min`
  }
}
