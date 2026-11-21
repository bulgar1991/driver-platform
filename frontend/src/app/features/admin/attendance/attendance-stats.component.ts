import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core'
import { RouterLink } from '@angular/router'
import { MatFormField, MatLabel } from '@angular/material/form-field'
import { MatInput } from '@angular/material/input'
import { MatOption, MatSelect } from '@angular/material/select'
import { finalize } from 'rxjs'
import {
  AttendanceCalendar,
  AttendanceEntry,
  AttendancePerson,
  AttendanceType,
  DriverContractType,
} from '@/core/models'
import { AttendanceService } from '@/core/services/attendance.service'
import { downloadCsv } from '@/core/utils/csv'
import { todayInOffice } from '@/core/utils/local-time'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { CONTRACT_TYPES } from '@/features/admin/staff-labels'
import {
  addDays,
  daysBetween,
  entryMinutes,
  formatMinutes,
  shortTime,
} from './attendance-utils'

/** The API allows at most this many days at once. */
const MAX_DAYS = 400

interface Row {
  entry: AttendanceEntry
  person: AttendancePerson
  type: AttendanceType
  /** Null for a whole day. */
  minutes: number | null
}

interface Total {
  type: AttendanceType
  days: number
  minutes: number
}

/** Admin → Attendance → Statistics: every event in a period, with durations and totals per kind. */
@Component({
  selector: 'app-attendance-stats',
  imports: [
    DatePipe,
    RouterLink,
    MatFormField,
    MatLabel,
    MatInput,
    MatSelect,
    MatOption,
    ButtonComponent,
    IconComponent,
  ],
  templateUrl: './attendance-stats.component.html',
  styleUrl: './attendance-stats.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AttendanceStatsComponent implements OnInit {
  private readonly api = inject(AttendanceService)
  protected readonly contractTypes = CONTRACT_TYPES
  protected readonly wholeDay = $localize`Whole day`
  protected readonly formatMinutes = formatMinutes
  protected readonly shortTime = shortTime

  protected readonly from = signal(todayInOffice().slice(0, 4) + '-01-01')
  protected readonly to = signal(todayInOffice())
  protected readonly data = signal<AttendanceCalendar | null>(null)
  protected readonly loading = signal(true)
  protected readonly error = signal<string | null>(null)

  protected readonly driverId = signal('')
  protected readonly contract = signal<DriverContractType | ''>('')
  protected readonly jobFunction = signal('')
  protected readonly typeId = signal('')

  protected readonly people = computed(() => this.data()?.people ?? [])
  protected readonly types = computed(() => this.data()?.types ?? [])
  protected readonly functions = computed(() =>
    [
      ...new Set(
        this.people()
          .map(p => p.jobFunction)
          .filter((f): f is string => !!f)
      ),
    ].sort()
  )

  protected readonly rows = computed<Row[]>(() => {
    const people = new Map(this.people().map(p => [p.id, p]))
    const types = new Map(this.types().map(t => [t.id, t]))
    const rows: Row[] = []
    for (const entry of this.data()?.entries ?? []) {
      const person = people.get(entry.driverId)
      const type = types.get(entry.typeId)
      if (
        !person ||
        !type ||
        (this.driverId() && person.id !== this.driverId()) ||
        (this.contract() && person.contractType !== this.contract()) ||
        (this.jobFunction() && person.jobFunction !== this.jobFunction()) ||
        (this.typeId() && type.id !== this.typeId())
      ) {
        continue
      }
      rows.push({ entry, person, type, minutes: entryMinutes(entry) })
    }
    return rows.sort(
      (a, b) =>
        a.entry.date.localeCompare(b.entry.date) ||
        a.person.lastName.localeCompare(b.person.lastName)
    )
  })

  protected readonly totals = computed<Total[]>(() => {
    const byType = new Map<string, Total>()
    for (const r of this.rows()) {
      const total = byType.get(r.type.id) ?? {
        type: r.type,
        days: 0,
        minutes: 0,
      }
      if (r.minutes === null) {
        total.days++
      } else {
        total.minutes += r.minutes
      }
      byType.set(r.type.id, total)
    }
    return [...byType.values()].sort(
      (a, b) => a.type.sortOrder - b.type.sortOrder
    )
  })

  protected readonly wholeDays = computed(
    () => this.rows().filter(r => r.minutes === null).length
  )
  protected readonly hours = computed(() =>
    this.rows().reduce((sum, r) => sum + (r.minutes ?? 0), 0)
  )

  ngOnInit(): void {
    this.load()
  }

  protected setPeriod(from: string, to: string): void {
    if (!from || !to || to < from) {
      return
    }
    this.from.set(from)
    this.to.set(
      daysBetween(from, to).length > MAX_DAYS ? addDays(from, MAX_DAYS - 1) : to
    )
    this.load()
  }

  protected export(): void {
    const header = [
      $localize`Driver`,
      $localize`Date`,
      $localize`Event`,
      $localize`Begin`,
      $localize`End`,
      $localize`Duration`,
      $localize`Notes`,
    ]
    const rows = this.rows().map(r => [
      `${r.person.lastName} ${r.person.firstName}`,
      r.entry.date,
      r.type.name,
      shortTime(r.entry.startTime),
      shortTime(r.entry.endTime),
      r.minutes === null ? this.wholeDay : formatMinutes(r.minutes),
      r.entry.notes ?? '',
    ])
    downloadCsv(`attendance-statistics-${this.from()}-${this.to()}.csv`, [
      header,
      ...rows,
    ])
  }

  private load(): void {
    this.loading.set(true)
    this.error.set(null)
    this.api
      .calendar(this.from(), this.to())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: data => this.data.set(data),
        error: err => this.error.set(problemMessage(err)),
      })
  }
}
