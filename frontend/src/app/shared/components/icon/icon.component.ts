import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
} from '@angular/core'
import { IconName } from '@/core/models'
import { ICONS } from './icons'

@Component({
  selector: 'app-icon',
  templateUrl: './icon.component.html',
  styleUrl: './icon.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'icon', 'aria-hidden': 'true' },
})
export class IconComponent {
  readonly name = input.required<IconName>()
  protected readonly paths = computed(() => ICONS[this.name()])
}
