import { ChangeDetectionStrategy, Component, input } from '@angular/core'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { ImageComponent } from '@/shared/components/image/image.component'
import { SectionHeadingComponent } from '@/shared/components/section-heading/section-heading.component'
import { RevealDirective } from '@/shared/directives/reveal.directive'
import { CONTACT, PARTNER_LOGOS, PARTNER_SEGMENTS } from '../../home.content'

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
  protected readonly segments = [...PARTNER_SEGMENTS, ...PARTNER_SEGMENTS]
  protected readonly hasLogos = PARTNER_LOGOS.length > 0
  protected readonly contactEmail = CONTACT.email
  protected readonly docsUrl = '/api/partner/openapi/partner.json'
}
