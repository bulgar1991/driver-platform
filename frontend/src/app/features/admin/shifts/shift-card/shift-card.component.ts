import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
  signal,
} from '@angular/core'
import { FormsModule } from '@angular/forms'
import { AssignmentStatus, Shift } from '@/core/models'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { StatusBadgeComponent } from '@/shared/components/status-badge/status-badge.component'

export interface ShiftStatusChange {
  shift: Shift
  status: AssignmentStatus
  reason?: string
}

@Component({
  selector: 'app-shift-card',
  imports: [DatePipe, FormsModule, ButtonComponent, StatusBadgeComponent],
  templateUrl: './shift-card.component.html',
  styleUrl: './shift-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShiftCardComponent {
  readonly shift = input.required<Shift>()
  readonly busy = input(false)
  readonly edit = output<Shift>()
  readonly statusChange = output<ShiftStatusChange>()

  protected readonly cancelling = signal(false)
  protected reason = ''

  protected readonly open = computed(() =>
    ['Draft', 'Sent', 'Seen', 'Confirmed'].includes(this.shift().status)
  )

  protected readonly sendLabel = computed(() =>
    this.shift().sentAt ? 'Send again' : 'Send to driver'
  )

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
