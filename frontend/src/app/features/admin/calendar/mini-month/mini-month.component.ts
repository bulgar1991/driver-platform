import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  output,
  signal,
  untracked,
} from '@angular/core'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { addDays, mondayOf } from '../calendar-layout'

interface Cell {
  date: string
  day: number
  inMonth: boolean
  weekend: boolean
}

/** A small month grid: today, the shown period and weekends marked; a click picks the day. */
@Component({
  selector: 'app-mini-month',
  imports: [DatePipe, IconComponent],
  templateUrl: './mini-month.component.html',
  styleUrl: './mini-month.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MiniMonthComponent {
  /** The day the calendar is on. */
  readonly anchor = input.required<string>()
  readonly from = input.required<string>()
  readonly to = input.required<string>()
  readonly today = input.required<string>()
  readonly picked = output<string>()

  /** First of the month shown (follows the anchor, browsable with the arrows). */
  protected readonly month = signal('')
  protected readonly weekdays = computed(() =>
    Array.from({ length: 7 }, (_, i) => addDays('2026-11-16', i))
  )
  protected readonly cells = computed<Cell[]>(() => {
    const first = this.month()
    if (!first) {
      return []
    }
    const start = mondayOf(first)
    const m = first.slice(0, 7)
    return Array.from({ length: 42 }, (_, i) => {
      const date = addDays(start, i)
      const dow = new Date(date + 'T00:00:00Z').getUTCDay()
      return {
        date,
        day: Number(date.slice(8)),
        inMonth: date.startsWith(m),
        weekend: dow === 0 || dow === 6,
      }
    })
  })

  constructor() {
    effect(() => {
      const a = this.anchor()
      untracked(() => this.month.set(a.slice(0, 8) + '01'))
    })
  }

  protected browse(step: number): void {
    const [y, m] = this.month().split('-').map(Number)
    this.month.set(
      new Date(Date.UTC(y, m - 1 + step, 1)).toISOString().slice(0, 10)
    )
  }
}
