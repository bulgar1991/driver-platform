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
import { Meta } from '@angular/platform-browser'
import { filter, Observable, Subscription } from 'rxjs'
import { DriverPortal, AssignmentStatus } from '@/core/models'
import { DriverPortalService } from '@/core/services/driver-portal.service'
import { poll } from '@/core/utils/poll'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { LogoComponent } from '@/shared/components/logo/logo.component'
import { StatusBadgeComponent } from '@/shared/components/status-badge/status-badge.component'
import {
  DriverRideComponent,
  RideAction,
} from './driver-ride/driver-ride.component'
import { CarLoaderComponent } from '@/shared/components/car-loader/car-loader.component'

/** /driver/:token — the driver's personal link with every shift sent to them. Refreshes every minute. */
@Component({
  selector: 'app-driver-portal',
  imports: [
    CarLoaderComponent,
    DatePipe,
    ButtonComponent,
    LogoComponent,
    StatusBadgeComponent,
    DriverRideComponent,
  ],
  templateUrl: './driver-portal.component.html',
  styleUrl: './driver-portal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DriverPortalComponent {
  protected readonly t = {
    confirming: $localize`Confirming…`,
    confirm: $localize`Confirm this shift`,
  }
  protected readonly shiftStatus: Record<AssignmentStatus, string> = {
    Draft: $localize`Draft`,
    Sent: $localize`:shift status, not yet confirmed by the driver:New`,
    Seen: $localize`:shift status, not yet confirmed by the driver:New`,
    Confirmed: $localize`Confirmed`,
    Completed: $localize`Completed`,
    Cancelled: $localize`Cancelled`,
  }
  readonly token = input.required<string>()

  private readonly api = inject(DriverPortalService)
  private readonly destroyRef = inject(DestroyRef)
  private refresh?: Subscription

  protected readonly portal = signal<DriverPortal | null>(null)
  protected readonly loading = signal(true)
  protected readonly error = signal<string | null>(null)
  protected readonly invalid = signal(false)
  protected readonly busy = signal<string | null>(null)

  constructor() {
    // Personal data: keep the page out of search engines.
    inject(Meta).updateTag({ name: 'robots', content: 'noindex, nofollow' })
    effect(() => {
      const token = this.token()
      untracked(() => {
        this.refresh?.unsubscribe()
        this.refresh = poll(
          () => this.api.get(token),
          60_000,
          err => this.fail(err)
        )
          // Do not overwrite the screen while an action is in progress.
          .pipe(
            filter(() => this.busy() === null),
            takeUntilDestroyed(this.destroyRef)
          )
          .subscribe(portal => {
            this.loading.set(false)
            this.error.set(null)
            this.portal.set(portal)
          })
      })
    })
  }

  protected confirm(shiftId: string): void {
    this.run(shiftId, this.api.confirm(this.token(), shiftId))
  }

  protected rideAction(bookingId: string, action: RideAction): void {
    this.run(
      bookingId,
      action === 'pickedUp'
        ? this.api.pickedUp(this.token(), bookingId)
        : this.api.completed(this.token(), bookingId)
    )
  }

  private run(key: string, call: Observable<DriverPortal>): void {
    this.busy.set(key)
    this.error.set(null)
    call.subscribe({
      next: portal => {
        this.busy.set(null)
        this.portal.set(portal)
      },
      error: err => {
        this.busy.set(null)
        this.error.set(problemMessage(err))
      },
    })
  }

  private fail(err: unknown): void {
    this.loading.set(false)
    if ((err as { status?: number }).status === 404) {
      this.invalid.set(true)
    } else {
      this.error.set(problemMessage(err))
    }
  }
}
