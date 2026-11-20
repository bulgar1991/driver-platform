import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
} from '@angular/core'
import { RouterLink } from '@angular/router'
import { IconName } from '@/core/models'
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
    RouterLink,
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

  /** Partners page only: what a partner gets, how to start, and the API in short. */
  protected readonly benefits: {
    icon: IconName
    title: string
    text: string
  }[] = [
    {
      icon: 'tag',
      title: $localize`Partner rates`,
      text: $localize`An agreed discount on every ride, on top of our fixed prices.`,
    },
    {
      icon: 'wallet',
      title: $localize`One invoice a month`,
      text: $localize`No card payment per ride: all transfers on one monthly invoice.`,
    },
    {
      icon: 'chat',
      title: $localize`A personal contact`,
      text: $localize`One dispatcher who knows your guests and what they need.`,
    },
    {
      icon: 'route',
      title: $localize`Booking API`,
      text: $localize`Send bookings straight from your own system, no typing twice.`,
    },
  ]
  protected readonly steps = [
    {
      title: $localize`Get in touch`,
      text: $localize`Tell us about your company and how many transfers you expect.`,
    },
    {
      title: $localize`Agree your rate`,
      text: $localize`We send you an agreement with your rate and the invoicing details.`,
    },
    {
      title: $localize`Start booking`,
      text: $localize`Book by e-mail or phone, or connect your system with the API key we give you.`,
    },
  ]
  protected readonly endpoints = [
    {
      method: 'POST',
      path: '/api/partner/v1/quotes',
      text: $localize`Prices for every vehicle class, with your rate applied`,
    },
    {
      method: 'POST',
      path: '/api/partner/v1/bookings',
      text: $localize`Create a booking`,
    },
    {
      method: 'GET',
      path: '/api/partner/v1/bookings/{reference}',
      text: $localize`Status of a booking and its driver`,
    },
    {
      method: 'GET',
      path: '/api/partner/v1/bookings?updatedSince=',
      text: $localize`Bookings changed since a given time`,
    },
    {
      method: 'POST',
      path: '/api/partner/v1/bookings/{reference}/cancel',
      text: $localize`Cancel a booking`,
    },
  ]
}
