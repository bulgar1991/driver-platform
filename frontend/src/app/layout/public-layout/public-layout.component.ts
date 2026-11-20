import { ChangeDetectionStrategy, Component, inject } from '@angular/core'
import { RouterOutlet } from '@angular/router'
import { ConsentService } from '@/core/services/consent.service'
import { VisitTrackerService } from '@/core/services/visit-tracker.service'
import { CookieBannerComponent } from '../cookie-banner/cookie-banner.component'
import { SiteFooterComponent } from '../site-footer/site-footer.component'
import { SiteHeaderComponent } from '../site-header/site-header.component'

@Component({
  selector: 'app-public-layout',
  imports: [
    RouterOutlet,
    SiteHeaderComponent,
    SiteFooterComponent,
    CookieBannerComponent,
  ],
  templateUrl: './public-layout.component.html',
  styleUrl: './public-layout.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PublicLayoutComponent {
  constructor() {
    // Google Tag Manager on the public pages, when this site has a container (GTM_ID).
    inject(ConsentService).start()
    // Our own visitor statistics (Admin → Visitors).
    inject(VisitTrackerService).start()
  }
}
