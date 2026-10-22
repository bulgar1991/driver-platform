import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core'
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router'
import { AuthService } from '@/core/services/auth.service'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { LogoComponent } from '@/shared/components/logo/logo.component'
import { StaffRole } from '@/core/models'
import { LanguageSwitchComponent } from '../language-switch/language-switch.component'
import { ADMIN_NAVIGATION } from './admin-navigation'

@Component({
  selector: 'app-admin-layout',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    IconComponent,
    LanguageSwitchComponent,
    LogoComponent,
  ],
  templateUrl: './admin-layout.component.html',
  styleUrl: './admin-layout.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminLayoutComponent {
  private readonly auth = inject(AuthService)

  protected readonly user = this.auth.currentUser
  protected readonly menuOpen = signal(false)
  protected readonly roleLabels: Record<StaffRole, string> = {
    Admin: $localize`Admin`,
    Dispatcher: $localize`Dispatcher`,
  }
  protected readonly navigation = computed(() =>
    ADMIN_NAVIGATION.filter(
      item => !item.roles || this.auth.hasRole(...item.roles)
    )
  )

  protected toggleMenu(): void {
    this.menuOpen.update(open => !open)
  }

  protected logout(): void {
    this.auth.logout()
  }
}
