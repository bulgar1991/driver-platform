import { ChangeDetectionStrategy, Component } from '@angular/core'
import { RouterLink } from '@angular/router'
import { SectionHeadingComponent } from '@/shared/components/section-heading/section-heading.component'
import { ServiceCardComponent } from '@/shared/components/service-card/service-card.component'
import { RevealDirective } from '@/shared/directives/reveal.directive'
import { SERVICES } from '../../home.content'

@Component({
  selector: 'app-services-section',
  imports: [
    RouterLink,
    SectionHeadingComponent,
    ServiceCardComponent,
    RevealDirective,
  ],
  templateUrl: './services-section.component.html',
  styleUrl: './services-section.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServicesSectionComponent {
  protected readonly services = SERVICES
}
