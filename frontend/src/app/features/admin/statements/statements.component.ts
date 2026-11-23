import { DatePipe, DecimalPipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core'
import { ActivatedRoute, Router, RouterLink } from '@angular/router'
import { MatFormField, MatLabel } from '@angular/material/form-field'
import { MatInput } from '@angular/material/input'
import { Drawer } from 'primeng/drawer'
import { finalize, Observable } from 'rxjs'
import {
  StatementDetail,
  StatementMonth,
  StatementRow,
  StatementStatus,
} from '@/core/models'
import { AdminStatementsService } from '@/core/services/admin-statements.service'
import { ToastService } from '@/core/services/toast.service'
import { saveBlob } from '@/core/utils/download'
import { todayInOffice } from '@/core/utils/local-time'
import { problemMessage } from '@/core/utils/problem'
import {
  BOOKING_STATUS_LABEL,
  BOOKING_STATUS_TONE,
} from '@/core/utils/status-labels'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { DialogService } from '@/shared/components/dialog/dialog.service'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { BookingDetailComponent } from '@/features/admin/bookings/booking-detail/booking-detail.component'

type Tone = 'neutral' | 'info' | 'warning' | 'success' | 'danger'

/**
 * Admin → Statements: what each partner owes for a month — the rides at their price, less the agreed
 * commission. A statement is issued (numbered and frozen, with its PDF), emailed and marked paid; if
 * the rides change after issuing, the row says so and it can be issued again with the same number.
 */
@Component({
  selector: 'app-statements',
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    MatFormField,
    MatLabel,
    MatInput,
    Drawer,
    ButtonComponent,
    IconComponent,
    BookingDetailComponent,
  ],
  templateUrl: './statements.component.html',
  styleUrl: './statements.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatementsComponent implements OnInit {
  protected readonly bookingStatusLabel = BOOKING_STATUS_LABEL
  protected readonly bookingStatusTone = BOOKING_STATUS_TONE
  private readonly api = inject(AdminStatementsService)
  private readonly toast = inject(ToastService)
  private readonly dialog = inject(DialogService)
  private readonly router = inject(Router)
  private readonly route = inject(ActivatedRoute)

  protected readonly today = todayInOffice()
  protected readonly statusLabel: Record<StatementStatus, string> = {
    NotIssued: $localize`Not issued`,
    Issued: $localize`Issued`,
    Sent: $localize`Sent`,
    Overdue: $localize`Overdue`,
    Paid: $localize`Paid`,
  }
  protected readonly statusTone: Record<StatementStatus, Tone> = {
    NotIssued: 'neutral',
    Issued: 'info',
    Sent: 'warning',
    Overdue: 'danger',
    Paid: 'success',
  }

  /** "yyyy-MM" of the month shown; the last month by default, the one usually billed. */
  protected readonly month = signal(previousMonth(this.today))
  protected readonly data = signal<StatementMonth | null>(null)
  protected readonly loading = signal(false)
  protected readonly error = signal<string | null>(null)
  /** Partner id of the row being worked on, to disable its buttons. */
  protected readonly busy = signal<string | null>(null)

  protected readonly monthLabel = computed(() => monthName(this.month()))
  protected readonly isCurrentMonth = computed(
    () => this.month() >= this.today.slice(0, 7)
  )

  // ── The drawer ─────────────────────────────────────────────────────────────
  protected readonly openId = signal<string | null>(null)
  protected readonly detail = signal<StatementDetail | null>(null)
  protected readonly detailLoading = signal(false)
  protected readonly detailError = signal<string | null>(null)
  protected readonly sendTo = signal('')
  protected readonly paidOn = signal(this.today)
  protected readonly note = signal('')
  /** A ride opened from the list: the drawer shows the booking, with a way back. */
  protected readonly rideId = signal<string | null>(null)

  ngOnInit(): void {
    const m = this.route.snapshot.queryParamMap.get('month')
    if (m && /^\d{4}-\d{2}$/.test(m)) this.month.set(m)
    this.load()
    const open = this.route.snapshot.queryParamMap.get('open')
    if (open) this.open(open)
  }

  protected shift(months: number): void {
    this.setMonth(addMonths(this.month(), months))
  }

  protected setMonth(value: string): void {
    if (!/^\d{4}-\d{2}$/.test(value)) return
    this.month.set(value)
    this.close()
    this.load()
  }

  protected hasRides(row: StatementRow): boolean {
    return row.rides + row.cancelled > 0
  }

  // ── Actions (the table and the drawer share them) ───────────────────────────

  protected issue(row: StatementRow): void {
    const again = row.status !== 'NotIssued'
    const go = () =>
      this.run(row.partnerId, this.api.issue(row.partnerId, ...this.ym()), d =>
        this.toast.success(
          again
            ? $localize`Statement ${d.row.number}:number: issued again.`
            : $localize`Statement ${d.row.number}:number: issued.`
        )
      )
    if (!again && this.isCurrentMonth()) {
      this.dialog
        .confirm({
          title: $localize`Issue before the month is over?`,
          message: $localize`Rides can still be added to ${this.monthLabel()}:month:. If they are, the statement can be issued again with the same number.`,
          confirm: $localize`Issue`,
        })
        .subscribe(ok => ok && go())
    } else if (again && row.sentAt) {
      this.dialog
        .confirm({
          title: $localize`Issue ${row.number}:number: again?`,
          message: $localize`The partner already received this statement. The new copy replaces it and has to be sent again.`,
          confirm: $localize`Issue again`,
        })
        .subscribe(ok => ok && go())
    } else {
      go()
    }
  }

  protected send(row: StatementRow, to?: string): void {
    const address = (to ?? row.contactEmail).trim()
    this.dialog
      .confirm({
        title: $localize`Send ${row.number}:number:?`,
        message: $localize`The statement goes by email, PDF attached, to ${address}:email:.`,
        confirm: $localize`Send`,
      })
      .subscribe(ok => {
        if (!ok) return
        this.run(
          row.partnerId,
          this.api.send(
            row.partnerId,
            ...this.ym(),
            address === row.contactEmail ? null : address
          ),
          () =>
            this.toast.success($localize`Statement sent to ${address}:email:.`)
        )
      })
  }

  /** From the drawer, with the date and a note. */
  protected markPaid(row: StatementRow, on: string, note: string): void {
    this.run(
      row.partnerId,
      this.api.setPaid(
        row.partnerId,
        ...this.ym(),
        true,
        on || null,
        note.trim() || null
      ),
      d =>
        this.toast.success($localize`${d.row.number}:number: marked as paid.`)
    )
  }

  /** From the table: paid today, after a confirmation. */
  protected quickPaid(row: StatementRow): void {
    this.dialog
      .confirm({
        title: $localize`Mark ${row.number}:number: as paid?`,
        message: $localize`Paid today. To give another date or a note, open the statement.`,
        confirm: $localize`Mark as paid`,
      })
      .subscribe(ok => ok && this.markPaid(row, this.today, row.note ?? ''))
  }

  protected undoPaid(row: StatementRow): void {
    this.dialog
      .confirm({
        title: $localize`Undo the payment?`,
        message: $localize`${row.number}:number: goes back to waiting for payment.`,
        confirm: $localize`Undo`,
      })
      .subscribe(ok => {
        if (!ok) return
        this.run(
          row.partnerId,
          this.api.setPaid(row.partnerId, ...this.ym(), false, null, row.note),
          () => this.toast.success($localize`Payment undone.`)
        )
      })
  }

  protected download(row: StatementRow, kind: 'pdf' | 'csv'): void {
    this.busy.set(row.partnerId)
    this.api
      .file(row.partnerId, ...this.ym(), kind)
      .pipe(finalize(() => this.busy.set(null)))
      .subscribe({
        next: blob => {
          const name =
            row.number ??
            `statement-${slug(row.partnerName)}-${this.month()}${row.status === 'NotIssued' && kind === 'pdf' ? '-draft' : ''}`
          saveBlob(blob, `${name}.${kind}`)
        },
        error: err => this.toast.error(problemMessage(err)),
      })
  }

  // ── Drawer ────────────────────────────────────────────────────────────────

  protected open(partnerId: string): void {
    this.openId.set(partnerId)
    this.rideId.set(null)
    this.detail.set(null)
    this.detailError.set(null)
    this.detailLoading.set(true)
    this.syncUrl()
    this.api
      .get(partnerId, ...this.ym())
      .pipe(finalize(() => this.detailLoading.set(false)))
      .subscribe({
        next: d => this.showDetail(d),
        error: err => this.detailError.set(problemMessage(err)),
      })
  }

  protected close(): void {
    if (this.openId() === null) return
    this.openId.set(null)
    this.rideId.set(null)
    this.syncUrl()
  }

  private showDetail(d: StatementDetail): void {
    this.detail.set(d)
    this.sendTo.set(d.row.sentTo ?? d.row.contactEmail)
    this.note.set(d.row.note ?? '')
    this.paidOn.set(d.row.paidAt?.slice(0, 10) ?? this.today)
  }

  /** Runs an action on a row, then shows its new state in the table and the drawer. */
  private run(
    partnerId: string,
    action: Observable<StatementDetail>,
    done: (d: StatementDetail) => void
  ): void {
    this.busy.set(partnerId)
    action.pipe(finalize(() => this.busy.set(null))).subscribe({
      next: d => {
        if (this.openId() === partnerId) this.showDetail(d)
        done(d)
        this.load()
      },
      error: err => this.toast.error(problemMessage(err)),
    })
  }

  private ym(): [number, number] {
    const [y, m] = this.month().split('-').map(Number)
    return [y, m]
  }

  private syncUrl(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        month: this.month() === previousMonth(this.today) ? null : this.month(),
        open: this.openId(),
      },
      replaceUrl: true,
    })
  }

  private load(): void {
    this.syncUrl()
    this.loading.set(true)
    this.error.set(null)
    this.api
      .month(...this.ym())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: d => this.data.set(d),
        error: err => this.error.set(problemMessage(err)),
      })
  }
}

export function addMonths(month: string, n: number): string {
  const [y, m] = month.split('-').map(Number)
  const d = new Date(Date.UTC(y, m - 1 + n, 1))
  return `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, '0')}`
}

export function previousMonth(today: string): string {
  return addMonths(today.slice(0, 7), -1)
}

function monthName(month: string): string {
  const [y, m] = month.split('-').map(Number)
  const name = new Date(Date.UTC(y, m - 1, 1)).toLocaleDateString(
    document.documentElement.lang || undefined,
    { month: 'long', year: 'numeric', timeZone: 'UTC' }
  )
  return name.charAt(0).toUpperCase() + name.slice(1)
}

function slug(name: string): string {
  return name
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '')
}
