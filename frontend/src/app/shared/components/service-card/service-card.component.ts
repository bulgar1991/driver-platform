import { ChangeDetectionStrategy, Component, input } from '@angular/core'
import { ServiceOffering } from '@/core/models'
import { IconComponent } from '../icon/icon.component'

@Component({
  selector: 'app-service-card',
  imports: [IconComponent],
  templateUrl: './service-card.component.html',
  styleUrl: './service-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServiceCardComponent {
  readonly service = input.required<ServiceOffering>()
}
