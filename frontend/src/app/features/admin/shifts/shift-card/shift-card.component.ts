import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core'
import { FormsModule } from '@angular/forms'
import { AdminDispatchService } from '@/core/services/admin-dispatch.service'
import { ToastService } from '@/core/services/toast.service'
import { MatFormField } from '@angular/material/form-field'
import { MatInput } from '@angular/material/input'
import { AssignmentStatus, Shift } from '@/core/models'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { StatusBadgeComponent } from '@/shared/components/status-badge/status-badge.component'
import { ADMIN_TEXT } from '@/features/admin/admin-texts'

export interface ShiftStatusChange {
  shift: Shift
  status: AssignmentStatus
  reason?: string
}

@Component({
  selector: 'app-shift-card',
  imports: [
    MatFormField,
    MatInput,
    DatePipe,
    FormsModule,
    ButtonComponent,
    StatusBadgeComponent,
  ],
  templateUrl: './shift-card.component.html',
  styleUrl: './shift-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShiftCardComponent {
  protected readonly text = ADMIN_TEXT
  protected readonly tx = {
    messageStatus: {
      Sent: '✓',
      Failed: $localize`✗ failed`,
      Skipped: $localize`– skipped`,
    } as Record<string, string>,
  }

  readonly shift = input.required<Shift>()
  readonly busy = input(false)
  readonly edit = output<Shift>()
  readonly statusChange = output<ShiftStatusChange>()

  private readonly api = inject(AdminDispatchService)
  private readonly toast = inject(ToastService)

  protected readonly cancelling = signal(false)
  protected reason = ''

  protected readonly open = computed(() =>
    ['Draft', 'Sent', 'Seen', 'Confirmed'].includes(this.shift().status)
  )

  protected readonly sendLabel = computed(() =>
    this.shift().sentAt ? $localize`Send again` : $localize`Send to driver`
  )

  /**
   * Opens WhatsApp (app or WhatsApp Web) on the driver's number with the shift written out: the office
   * presses send. The tab opens on the click itself, so the browser does not block it as a popup.
   */
  protected whatsApp(): void {
    const tab = window.open('', '_blank')
    this.api.shiftWhatsApp(this.shift().id).subscribe({
      next: m => {
        const url = `https://wa.me/${m.phone}?text=${encodeURIComponent(m.text)}`
        if (tab) tab.location.href = url
        else window.location.href = url
      },
      error: () => {
        tab?.close()
        this.toast.error($localize`Could not prepare the WhatsApp message.`)
      },
    })
  }

  protected change(status: AssignmentStatus): void {
    if (status === 'Cancelled') {
      if (!this.reason.trim()) {
        return
      }
      this.statusChange.emit({
        shift: this.shift(),
        status,
        reason: this.reason.trim(),
      })
      this.cancelling.set(false)
      this.reason = ''
      return
    }
    this.statusChange.emit({ shift: this.shift(), status })
  }
}
