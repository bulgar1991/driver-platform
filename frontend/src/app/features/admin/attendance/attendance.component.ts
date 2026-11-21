import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { RouterLink } from '@angular/router'
import { MatCheckbox } from '@angular/material/checkbox'
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field'
import { MatInput } from '@angular/material/input'
import { MatOption, MatSelect } from '@angular/material/select'
import { finalize, Observable } from 'rxjs'
import {
  AttendanceCalendar,
  AttendanceEntry,
  AttendancePerson,
  AttendanceType,
  DriverContractType,
} from '@/core/models'
import { AttendanceService } from '@/core/services/attendance.service'
import { AuthService } from '@/core/services/auth.service'
import { downloadCsv } from '@/core/utils/csv'
import { todayInOffice } from '@/core/utils/local-time'
import { problemMessage } from '@/core/utils/problem'
import {
  DialogComponent,
  DialogOpenDirective,
} from '@/shared/components/dialog'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { ADMIN_TEXT } from '@/features/admin/admin-texts'
import { CONTRACT_TYPES } from '@/features/admin/staff-labels'
import {
  addDays,
  daysBetween,
  employedOn,
  isWeekend,
  monthOf,
  shortTime,
} from './attendance-utils'

/** Longest period the grid shows at once. */
const MAX_DAYS = 62
const THRESHOLD_KEY = 'attendance.alertBelow'

interface CellEdit {
  person: AttendancePerson
  date: string
  entry: AttendanceEntry | null
}

/**
 * Admin → Attendance: one row per staff member, one column per day. A click on a day puts an event
 * there (holiday, sick leave…) or on a range of days; the bottom row counts who is available.
 */
@Component({
  selector: 'app-attendance',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatCheckbox,
    MatError,
    MatFormField,
    MatLabel,
    MatInput,
    MatSelect,
    MatOption,
    DialogComponent,
    DialogOpenDirective,
    ButtonComponent,
    IconComponent,
  ],
  templateUrl: './attendance.component.html',
  styleUrl: './attendance.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AttendanceComponent implements OnInit {
  protected readonly text = ADMIN_TEXT
  protected readonly manageTitle = $localize`Attendance events`
  protected readonly contractTypes = CONTRACT_TYPES
  protected readonly isAdmin = inject(AuthService).hasRole('Admin')
  private readonly api = inject(AttendanceService)
  private readonly fb = inject(FormBuilder).nonNullable

  protected readonly today = todayInOffice()
  protected readonly from = signal(monthOf(this.today).from)
  protected readonly to = signal(monthOf(this.today).to)
  protected readonly data = signal<AttendanceCalendar | null>(null)
  protected readonly loading = signal(true)
  protected readonly error = signal<string | null>(null)

  // Filters
  protected readonly contract = signal<DriverContractType | ''>('')
  protected readonly jobFunction = signal('')
  protected readonly operative = signal<'yes' | 'no' | 'all'>('yes')
  protected readonly typeFilter = signal('')
  protected readonly alertBelow = signal<number | null>(readThreshold())

  protected readonly days = computed(() => daysBetween(this.from(), this.to()))
  protected readonly types = computed(() => this.data()?.types ?? [])
  protected readonly typeById = computed(
    () => new Map(this.types().map(t => [t.id, t]))
  )
  protected readonly functions = computed(() =>
    [
      ...new Set(
        (this.data()?.people ?? [])
          .map(p => p.jobFunction)
          .filter((f): f is string => !!f)
      ),
    ].sort()
  )
  private readonly entryByCell = computed(
    () =>
      new Map(
        (this.data()?.entries ?? []).map(e => [`${e.driverId}|${e.date}`, e])
      )
  )

  protected readonly people = computed(() => {
    const contract = this.contract()
    const fn = this.jobFunction()
    const op = this.operative()
    const type = this.typeFilter()
    const entries = this.data()?.entries ?? []
    return (this.data()?.people ?? []).filter(
      p =>
        (!contract || p.contractType === contract) &&
        (!fn || p.jobFunction === fn) &&
        (op === 'all' || p.isActive === (op === 'yes')) &&
        (!type || entries.some(e => e.driverId === p.id && e.typeId === type))
    )
  })

  /** Per day: people shown, active, employed then and not off for the whole day. */
  protected readonly available = computed(() => {
    const types = this.typeById()
    return this.days().map(
      day =>
        this.people().filter(p => {
          if (!p.isActive || !employedOn(p, day)) {
            return false
          }
          const e = this.entry(p.id, day)
          return !(e && !e.startTime && types.get(e.typeId)?.makesUnavailable)
        }).length
    )
  })

  // Day editor
  protected readonly editing = signal<CellEdit | null>(null)
  protected readonly saving = signal(false)
  protected readonly formError = signal<string | null>(null)
  protected readonly form = this.fb.group({
    typeId: ['', Validators.required],
    from: ['', Validators.required],
    to: ['', Validators.required],
    wholeDay: [true],
    startTime: ['08:00'],
    endTime: ['12:00'],
    skipWeekends: [false],
    notes: ['', Validators.maxLength(500)],
  })

  // Kinds of event (admins)
  protected readonly managingTypes = signal(false)
  protected readonly editingType = signal<AttendanceType | null>(null)
  protected readonly typeError = signal<string | null>(null)
  protected readonly typeForm = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(60)]],
    code: [
      '',
      [Validators.required, Validators.pattern(/^[A-Za-z0-9/]{1,3}$/)],
    ],
    color: ['#22c55e', Validators.required],
    makesUnavailable: [true],
    sortOrder: [100, [Validators.min(0), Validators.max(999)]],
  })

  ngOnInit(): void {
    this.load()
  }

  protected entry(driverId: string, date: string): AttendanceEntry | null {
    return this.entryByCell().get(`${driverId}|${date}`) ?? null
  }

  protected type(entry: AttendanceEntry | null): AttendanceType | null {
    return entry ? (this.typeById().get(entry.typeId) ?? null) : null
  }

  protected cellTitle(person: AttendancePerson, date: string): string {
    const e = this.entry(person.id, date)
    const t = this.type(e)
    if (!e || !t) {
      return ''
    }
    const times = e.startTime
      ? ` ${shortTime(e.startTime)}–${shortTime(e.endTime)}`
      : ''
    return `${t.name}${times}${e.notes ? ' · ' + e.notes : ''}`
  }

  protected readonly isWeekend = isWeekend
  protected readonly employedOn = employedOn

  protected shiftMonth(step: number): void {
    const anchor = step < 0 ? addDays(this.from(), -1) : addDays(this.to(), 1)
    const month = monthOf(anchor)
    this.from.set(month.from)
    this.to.set(month.to)
    this.load()
  }

  protected thisMonth(): void {
    const month = monthOf(this.today)
    this.from.set(month.from)
    this.to.set(month.to)
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

  protected setAlert(value: string): void {
    const n = value === '' ? null : Math.max(0, Math.floor(Number(value)))
    this.alertBelow.set(Number.isFinite(n) ? n : null)
    try {
      if (n === null) {
        localStorage.removeItem(THRESHOLD_KEY)
      } else {
        localStorage.setItem(THRESHOLD_KEY, String(n))
      }
    } catch {
      // Private window: the threshold just isn't remembered.
    }
  }

  protected openCell(person: AttendancePerson, date: string): void {
    const entry = this.entry(person.id, date)
    this.formError.set(null)
    this.form.reset({
      typeId: entry?.typeId ?? this.types()[0]?.id ?? '',
      from: date,
      to: date,
      wholeDay: !entry?.startTime,
      startTime: shortTime(entry?.startTime ?? null) || '08:00',
      endTime: shortTime(entry?.endTime ?? null) || '12:00',
      skipWeekends: false,
      notes: entry?.notes ?? '',
    })
    this.editing.set({ person, date, entry })
  }

  protected save(): void {
    const cell = this.editing()
    if (!cell || this.form.invalid) {
      this.form.markAllAsTouched()
      return
    }
    const v = this.form.getRawValue()
    this.run(
      this.api.set({
        driverId: cell.person.id,
        typeId: v.typeId,
        from: v.from,
        to: v.to,
        startTime: v.wholeDay ? null : v.startTime,
        endTime: v.wholeDay ? null : v.endTime,
        skipWeekends: v.skipWeekends,
        notes: v.notes.trim() || null,
      })
    )
  }

  /** Removes the events of the dates in the form (the clicked day by default). */
  protected clear(): void {
    const cell = this.editing()
    const v = this.form.getRawValue()
    if (cell && v.from && v.to) {
      this.run(this.api.clear(cell.person.id, v.from, v.to))
    }
  }

  /** CSV of the grid as shown: codes per day, and the available count. */
  protected export(): void {
    const days = this.days()
    const header = [$localize`Surname`, $localize`Name`, ...days]
    const rows = this.people().map(p => [
      p.lastName,
      p.firstName,
      ...days.map(d => {
        const e = this.entry(p.id, d)
        const t = this.type(e)
        return t
          ? t.code +
              (e!.startTime
                ? ` ${shortTime(e!.startTime)}-${shortTime(e!.endTime)}`
                : '')
          : ''
      }),
    ])
    const total = [$localize`Available`, '', ...this.available().map(String)]
    downloadCsv(`attendance-${this.from()}-${this.to()}.csv`, [
      header,
      ...rows,
      total,
    ])
  }

  // Kinds of event

  protected editType(type: AttendanceType | null): void {
    this.typeError.set(null)
    this.editingType.set(type)
    this.typeForm.reset({
      name: type?.name ?? '',
      code: type?.code ?? '',
      color: type?.color ?? '#22c55e',
      makesUnavailable: type?.makesUnavailable ?? true,
      sortOrder: type?.sortOrder ?? (this.types().at(-1)?.sortOrder ?? 0) + 10,
    })
  }

  protected saveType(): void {
    if (this.typeForm.invalid) {
      this.typeForm.markAllAsTouched()
      return
    }
    const v = this.typeForm.getRawValue()
    this.runType(
      this.api.saveType(
        { ...v, name: v.name.trim(), code: v.code.trim() },
        this.editingType()?.id
      )
    )
  }

  protected deleteType(type: AttendanceType): void {
    this.runType(this.api.deleteType(type.id))
  }

  private runType(request: Observable<unknown>): void {
    this.saving.set(true)
    this.typeError.set(null)
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.editType(null)
        this.load()
      },
      error: err => this.typeError.set(problemMessage(err)),
    })
  }

  private run(request: Observable<unknown>): void {
    this.saving.set(true)
    this.formError.set(null)
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.editing.set(null)
        this.load()
      },
      error: err => this.formError.set(problemMessage(err)),
    })
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

function readThreshold(): number | null {
  try {
    const v = localStorage.getItem(THRESHOLD_KEY)
    return v === null ? null : Number(v)
  } catch {
    return null
  }
}
