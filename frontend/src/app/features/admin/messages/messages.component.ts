import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core'
import { takeUntilDestroyed } from '@angular/core/rxjs-interop'
import { ActivatedRoute, Router, RouterLink } from '@angular/router'
import { MatFormField, MatLabel } from '@angular/material/form-field'
import { MatInput } from '@angular/material/input'
import { MatOption, MatSelect } from '@angular/material/select'
import { Drawer } from 'primeng/drawer'
import {
  debounceTime,
  distinctUntilChanged,
  filter,
  finalize,
  Subject,
  switchMap,
} from 'rxjs'
import {
  MessageAudience,
  MessageItem,
  MessageList,
  MessageQuery,
  NotificationChannel,
  NotificationKind,
  NotificationStatus,
} from '@/core/models'
import { AdminMessagesService } from '@/core/services/admin-messages.service'
import { ToastService } from '@/core/services/toast.service'
import { todayInOffice } from '@/core/utils/local-time'
import { problemMessage } from '@/core/utils/problem'
import { DialogService } from '@/shared/components/dialog'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { addDays } from '@/features/admin/calendar/calendar-layout'
import { BookingDetailComponent } from '@/features/admin/bookings/booking-detail/booking-detail.component'

const PAGE_SIZE = 50

interface Preset {
  key: string
  label: string
  range: () => { from: string; to: string }
}

/** What a message was about, in the panel's words. */
export const MESSAGE_KIND_LABEL: Record<NotificationKind, string> = {
  DriverShift: $localize`New shift`,
  DriverShiftChanged: $localize`Shift changed`,
  ClientDriverDetails: $localize`Driver details`,
  ReviewRequest: $localize`Review request`,
  BookingConfirmation: $localize`Booking confirmation`,
  BookingRefund: $localize`Refund / cancellation`,
}

/**
 * Admin → Messages: every WhatsApp and email sent to drivers and clients, what failed and why, and
 * a way to send a shift, the driver details or a booking email again.
 */
@Component({
  selector: 'app-messages',
  imports: [
    DatePipe,
    RouterLink,
    MatFormField,
    MatLabel,
    MatInput,
    MatSelect,
    MatOption,
    Drawer,
    IconComponent,
    BookingDetailComponent,
  ],
  templateUrl: './messages.component.html',
  styleUrl: './messages.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MessagesComponent implements OnInit {
  protected readonly kindLabel = MESSAGE_KIND_LABEL
  private readonly api = inject(AdminMessagesService)
  private readonly toast = inject(ToastService)
  private readonly dialog = inject(DialogService)
  private readonly router = inject(Router)
  private readonly route = inject(ActivatedRoute)
  private readonly destroyRef = inject(DestroyRef)

  protected readonly today = todayInOffice()
  protected readonly presets: Preset[] = [
    {
      key: 'today',
      label: $localize`Today`,
      range: () => ({ from: this.today, to: this.today }),
    },
    {
      key: '7',
      label: $localize`Last 7 days`,
      range: () => ({ from: addDays(this.today, -6), to: this.today }),
    },
    {
      key: '30',
      label: $localize`Last 30 days`,
      range: () => ({ from: addDays(this.today, -29), to: this.today }),
    },
    {
      key: '90',
      label: $localize`Last 90 days`,
      range: () => ({ from: addDays(this.today, -89), to: this.today }),
    },
  ]
  protected readonly audiences: { value: MessageAudience; label: string }[] = [
    { value: 'Any', label: $localize`Everyone` },
    { value: 'Driver', label: $localize`Drivers` },
    { value: 'Client', label: $localize`Clients` },
  ]
  protected readonly channels: {
    value: NotificationChannel | null
    label: string
  }[] = [
    { value: null, label: $localize`All channels` },
    { value: 'WhatsApp', label: 'WhatsApp' },
    { value: 'Email', label: $localize`Email` },
  ]
  protected readonly kinds = Object.entries(MESSAGE_KIND_LABEL).map(
    ([value, label]) => ({ value: value as NotificationKind, label })
  )
  protected readonly statusLabel: Record<NotificationStatus, string> = {
    Sent: $localize`Sent`,
    Failed: $localize`Failed`,
    Skipped: $localize`Not sent`,
  }

  protected readonly from = signal('')
  protected readonly to = signal('')
  protected readonly search = signal('')
  protected readonly audience = signal<MessageAudience>('Any')
  protected readonly channel = signal<NotificationChannel | null>(null)
  protected readonly status = signal<NotificationStatus | null>(null)
  protected readonly kind = signal<NotificationKind | null>(null)
  protected readonly page = signal(1)
  protected readonly list = signal<MessageList | null>(null)
  protected readonly loading = signal(false)
  protected readonly error = signal<string | null>(null)
  /** Messages being sent again (button spinners). */
  protected readonly resending = signal<ReadonlySet<number>>(new Set())
  /** A booking opened from a row, in the side panel. */
  protected readonly bookingId = signal<string | null>(null)
  private readonly typed = new Subject<string>()

  protected readonly pages = computed(() => {
    const l = this.list()
    return l ? Math.max(1, Math.ceil(l.total / l.pageSize)) : 1
  })

  protected readonly activePreset = computed(
    () =>
      this.presets.find(p => {
        const r = p.range()
        return r.from === this.from() && r.to === this.to()
      })?.key ?? null
  )

  ngOnInit(): void {
    const p = this.route.snapshot.queryParamMap
    const fallback = this.presets[1].range()
    this.from.set(p.get('from') ?? fallback.from)
    this.to.set(p.get('to') ?? fallback.to)
    this.search.set(p.get('search') ?? '')
    this.audience.set((p.get('audience') as MessageAudience) ?? 'Any')
    this.channel.set(p.get('channel') as NotificationChannel | null)
    this.status.set(p.get('status') as NotificationStatus | null)
    this.kind.set(p.get('kind') as NotificationKind | null)
    this.typed
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(value => {
        this.search.set(value)
        this.reload()
      })
    this.load()
  }

  protected typedSearch(value: string): void {
    this.typed.next(value)
  }

  protected usePreset(preset: Preset): void {
    const r = preset.range()
    this.from.set(r.from)
    this.to.set(r.to)
    this.reload()
  }

  protected setFrom(value: string): void {
    this.from.set(value)
    if (value && this.to()) this.reload()
  }

  protected setTo(value: string): void {
    this.to.set(value)
    if (value && this.from()) this.reload()
  }

  protected setAudience(value: MessageAudience): void {
    this.audience.set(value)
    this.reload()
  }

  protected setChannel(value: NotificationChannel | null): void {
    this.channel.set(value)
    this.reload()
  }

  /** The cards act as a status filter; a second click clears it. */
  protected setStatus(value: NotificationStatus | null): void {
    this.status.set(this.status() === value ? null : value)
    this.reload()
  }

  protected setKind(value: NotificationKind | null): void {
    this.kind.set(value)
    this.reload()
  }

  protected goTo(page: number): void {
    this.page.set(Math.min(Math.max(page, 1), this.pages()))
    this.load()
  }

  protected isDriverMessage(m: MessageItem): boolean {
    return m.kind === 'DriverShift' || m.kind === 'DriverShiftChanged'
  }

  protected resend(m: MessageItem): void {
    const who = this.isDriverMessage(m) ? m.driverName : m.clientName
    this.dialog
      .confirm({
        title: $localize`Send again?`,
        message: $localize`“${this.kindLabel[m.kind]}:kind:” goes out again to ${who ?? m.recipient}:who:, on every channel it normally uses.`,
        confirm: $localize`Send again`,
      })
      .pipe(
        filter(Boolean),
        switchMap(() => {
          this.resending.update(s => new Set(s).add(m.id))
          return this.api.resend(m.id).pipe(
            finalize(() =>
              this.resending.update(s => {
                const next = new Set(s)
                next.delete(m.id)
                return next
              })
            )
          )
        })
      )
      .subscribe({
        next: () => {
          this.toast.success($localize`Message sent again.`)
          this.load()
        },
        error: err => this.toast.error(problemMessage(err)),
      })
  }

  private query(): MessageQuery {
    return {
      from: this.from(),
      to: this.to(),
      search: this.search().trim(),
      audience: this.audience(),
      channel: this.channel(),
      status: this.status(),
      kind: this.kind(),
      page: this.page(),
      pageSize: PAGE_SIZE,
    }
  }

  private reload(): void {
    this.page.set(1)
    this.load()
  }

  private load(): void {
    const q = this.query()
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        from: q.from,
        to: q.to,
        search: q.search || null,
        audience: q.audience === 'Any' ? null : q.audience,
        channel: q.channel,
        status: q.status,
        kind: q.kind,
      },
      replaceUrl: true,
    })
    this.loading.set(true)
    this.error.set(null)
    this.api
      .list(q)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: l => this.list.set(l),
        error: err => this.error.set(problemMessage(err)),
      })
  }
}
