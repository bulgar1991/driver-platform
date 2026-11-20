import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
} from '@angular/core'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { ImageComponent } from '@/shared/components/image/image.component'
import { SectionHeadingComponent } from '@/shared/components/section-heading/section-heading.component'
import { RevealDirective } from '@/shared/directives/reveal.directive'
import { CONTACT, PARTNER_LOGOS, PARTNER_SEGMENTS } from '../../home.content'
import { BRAND } from '@/core/brand'
import { SiteContentService } from '@/core/services/site-content.service'
import { HEADINGS } from '../../editable-content'

/**
 * "Our partners and clients": an endless marquee (pure CSS keyframes, the list is doubled so the loop
 * is seamless). Shows partner logos once there are any with permission, the kinds of partners until then.
 */
@Component({
  selector: 'app-partners',
  imports: [
    IconComponent,
    ImageComponent,
    SectionHeadingComponent,
    RevealDirective,
  ],
  templateUrl: './partners.component.html',
  styleUrl: './partners.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PartnersComponent {
  /** The section is the whole page. */
  readonly page = input(false)
  protected readonly logos = [...PARTNER_LOGOS, ...PARTNER_LOGOS]
  private readonly site = inject(SiteContentService)
  protected readonly heading = computed(() => ({
    eyebrow: this.site.text('partners.eyebrow', HEADINGS.partnersEyebrow),
    title: this.site.text('partners.title', HEADINGS.partnersTitle),
    lead: this.site.text('partners.lead', HEADINGS.partnersLead),
  }))
  /** As changed in Admin → Website content; doubled for the endless marquee. */
  protected readonly segments = computed(() => {
    const list = PARTNER_SEGMENTS.map((s, i) => ({
      ...s,
      title: this.site.text(`partners.${i + 1}.title`, s.title),
      text: this.site.text(`partners.${i + 1}.text`, s.text),
    }))
    return [...list, ...list]
  })
  protected readonly hasLogos = PARTNER_LOGOS.length > 0
  protected readonly brandName = BRAND.name
  protected readonly contactEmail = CONTACT.email
  protected readonly docsUrl = '/api/partner/openapi/partner.json'
}
