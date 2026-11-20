import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
} from '@angular/core'
import { RouterLink } from '@angular/router'
import { SectionHeadingComponent } from '@/shared/components/section-heading/section-heading.component'
import { ServiceCardComponent } from '@/shared/components/service-card/service-card.component'
import { RevealDirective } from '@/shared/directives/reveal.directive'
import { SERVICES } from '../../home.content'
import { SiteContentService } from '@/core/services/site-content.service'
import { HEADINGS } from '../../editable-content'

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

  private readonly site = inject(SiteContentService)
  protected readonly heading = computed(() => ({
    eyebrow: this.site.text('services.eyebrow', HEADINGS.servicesEyebrow),
    title: this.site.text('services.title', HEADINGS.servicesTitle),
  }))
  /** As changed in Admin → Website content, the built-in texts otherwise. */
  protected readonly services = computed(() =>
    SERVICES.slice(0, this.limit() ?? SERVICES.length).map((s, i) => ({
      ...s,
      title: this.site.text(`services.${i + 1}.title`, s.title),
      description: this.site.text(`services.${i + 1}.text`, s.description),
    }))
  )
  protected readonly more = computed(
    () => this.services().length < SERVICES.length
  )
}
