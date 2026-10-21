import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
} from '@angular/core'
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
  /** How many services to show; all when not set (the /services page). */
  readonly limit = input<number | undefined>(undefined)
  /** The section is the whole page. */
  readonly page = input(false)

  protected readonly services = computed(() =>
    SERVICES.slice(0, this.limit() ?? SERVICES.length)
  )
  protected readonly more = computed(
    () => this.services().length < SERVICES.length
  )
}
