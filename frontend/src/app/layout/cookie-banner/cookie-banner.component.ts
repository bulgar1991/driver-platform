import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from '@angular/core'
import { RouterLink } from '@angular/router'
import { ConsentService } from '@/core/services/consent.service'
import { ButtonComponent } from '@/shared/components/button/button.component'

/** Asks before Google Tag Manager may set analytics or ad cookies (Swiss FADP, EU GDPR). */
@Component({
  selector: 'app-cookie-banner',
  imports: [RouterLink, ButtonComponent],
  templateUrl: './cookie-banner.component.html',
  styleUrl: './cookie-banner.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CookieBannerComponent {
  protected readonly consent = inject(ConsentService)
  protected readonly open = computed(
    () =>
      this.consent.enabled &&
      (this.consent.choice() === null || this.consent.reopened())
  )
}
