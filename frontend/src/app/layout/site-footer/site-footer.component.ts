import { ChangeDetectionStrategy, Component } from '@angular/core'
import { RouterLink } from '@angular/router'
import { CONTACT, SERVICES, SOCIAL } from '@/features/home/home.content'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { LogoComponent } from '@/shared/components/logo/logo.component'
import { LanguageSwitchComponent } from '../language-switch/language-switch.component'
import { SITE_PAGES } from '../site-header/site-header.component'

@Component({
  selector: 'app-site-footer',
  imports: [RouterLink, IconComponent, LogoComponent, LanguageSwitchComponent],
  templateUrl: './site-footer.component.html',
  styleUrl: './site-footer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SiteFooterComponent {
  protected readonly year = new Date().getFullYear()
  protected readonly pages = SITE_PAGES
  protected readonly services = SERVICES
  protected readonly contact = CONTACT
  protected readonly social = SOCIAL
  protected readonly whatsAppUrl = CONTACT.whatsapp
    ? `https://wa.me/${CONTACT.whatsapp.replace(/\D/g, '')}`
    : null
}
