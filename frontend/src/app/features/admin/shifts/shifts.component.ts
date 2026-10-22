import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core'
import { takeUntilDestroyed } from '@angular/core/rxjs-interop'
import { FormsModule } from '@angular/forms'
import { Router } from '@angular/router'
import { Dialog } from 'primeng/dialog'
import { combineLatest, Subscription } from 'rxjs'
import { BookingListItem, Shift, ShiftResult } from '@/core/models'
import { AdminDispatchService } from '@/core/services/admin-dispatch.service'
import { todayInItaly } from '@/core/utils/italy-time'
import { poll } from '@/core/utils/poll'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import {
  ShiftCardComponent,
  ShiftStatusChange,
} from './shift-card/shift-card.component'
import { ShiftEditorComponent } from './shift-editor/shift-editor.component'
import { SHIFT_STATUS_LABEL } from '@/core/utils/status-labels'
import { ADMIN_TEXT } from '@/features/admin/admin-texts'

/** One day of dispatch: the rides still to assign on the left, the shifts on the right. */
@Component({
  selector: 'app-shifts',
  imports: [
    DatePipe,
    FormsModule,
    Dialog,
    ButtonComponent,
    IconComponent,
    ShiftCardComponent,
    ShiftEditorComponent,
  ],
  templateUrl: './shifts.component.html',
  styleUrl: './shifts.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShiftsComponent {
  protected readonly text = ADMIN_TEXT
  protected readonly tx = {
    editShift: $localize`Edit shift`,
    newShift: $localize`New shift`,
    newShiftWith: (reference: string) =>
      $localize`New shift with ${reference}:reference:`,
  }

  /** Query params: the day, and bookings ticked on the Bookings page. */
  readonly date = input<string | undefined>(undefined)
  readonly bookings = input<string | undefined>(undefined)

  private readonly api = inject(AdminDispatchService)
  private readonly router = inject(Router)
  private readonly destroyRef = inject(DestroyRef)
  private refresh?: Subscription

  protected readonly day = signal(todayInItaly())
  protected readonly shifts = signal<Shift[]>([])
  protected readonly unassigned = signal<BookingListItem[]>([])
  protected readonly loading = signal(true)
  protected readonly message = signal<{
    text: string
    error: boolean
    warnings?: string[]
  } | null>(null)
  protected readonly busyId = signal<string | null>(null)

  protected readonly editorOpen = signal(false)
  protected readonly editing = signal<Shift | null>(null)
  protected readonly preselected = signal<string[]>([])

  constructor() {
    effect(() => {
      const date = this.date()
      const bookings = this.bookings()
      untracked(() => {
        if (date && /^\d{4}-\d{2}-\d{2}$/.test(date)) {
          this.day.set(date)
        }
        if (bookings) {
          this.preselected.set(bookings.split(',').filter(Boolean))
          this.editing.set(null)
          this.editorOpen.set(true)
        }
        this.reload()
      })
    })
  }

  protected changeDay(value: string): void {
    if (value) {
      void this.router.navigate([], { queryParams: { date: value } })
    }
  }

  protected shiftDay(days: number): void {
    const d = new Date(`${this.day()}T12:00:00Z`)
    d.setUTCDate(d.getUTCDate() + days)
    this.changeDay(d.toISOString().slice(0, 10))
  }

  protected newShift(ids: string[] = []): void {
    this.editing.set(null)
    this.preselected.set(ids)
    this.editorOpen.set(true)
  }

  protected editShift(shift: Shift): void {
    this.editing.set(shift)
    this.preselected.set([])
    this.editorOpen.set(true)
  }

  protected closeEditor(): void {
    this.editorOpen.set(false)
    // Drop the "bookings" query param so a reload does not reopen the editor.
    if (this.bookings()) {
      void this.router.navigate([], { queryParams: { date: this.day() } })
    }
  }

  protected onSaved(result: ShiftResult): void {
    this.closeEditor()
    this.message.set({
      text: $localize`Shift for ${result.shift.driverName}:driver: saved.`,
      error: false,
      warnings: result.warnings,
    })
    this.reload()
  }

  protected onStatusChange(change: ShiftStatusChange): void {
    this.busyId.set(change.shift.id)
    this.api
      .setShiftStatus(change.shift.id, change.status, change.reason)
      .subscribe({
        next: () => {
          this.busyId.set(null)
          this.message.set({
            text:
              change.status === 'Sent'
                ? $localize`Shift sent to ${change.shift.driverName}:driver: on WhatsApp and email. The card shows what was delivered.`
                : $localize`Shift for ${change.shift.driverName}:driver:: ${SHIFT_STATUS_LABEL[change.status]}:status:.`,
            error: false,
          })
          this.reload()
        },
        error: err => {
          this.busyId.set(null)
          this.message.set({ text: problemMessage(err), error: true })
        },
      })
  }

  private reload(): void {
    this.refresh?.unsubscribe()
    const day = this.day()
    this.loading.set(true)
    this.refresh = poll(
      () =>
        combineLatest([
          this.api.shifts(day, day),
          this.api.bookings({
            from: day,
            to: day,
            unassigned: true,
            pageSize: 200,
          }),
        ]),
      undefined,
      err => this.message.set({ text: problemMessage(err), error: true })
    )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(([shifts, bookings]) => {
        this.loading.set(false)
        this.shifts.set(shifts)
        this.unassigned.set(bookings.items)
      })
  }
}
