import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
} from '@angular/core'
import { MeetingPoint, Zone } from '@/core/models'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { InstructionsPipe } from '@/shared/pipes/instructions.pipe'

/** Shown when the address is in a zone with meeting points (Venice, airports). */
@Component({
  selector: 'app-meeting-point-picker',
  imports: [IconComponent, InstructionsPipe],
  templateUrl: './meeting-point-picker.component.html',
  styleUrl: './meeting-point-picker.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MeetingPointPickerComponent {
  readonly zone = input.required<Zone>()
  readonly selected = input<MeetingPoint | null>(null)
  readonly direction = input<'pickup' | 'dropoff'>('pickup')
  readonly disabled = input(false)
  readonly changed = output<string>()

  protected readonly noCar = computed(() => this.zone().type === 'NoCarAccess')
  protected readonly groupName = computed(
    () => `meeting-${this.direction()}-${this.zone().id}`
  )
}
