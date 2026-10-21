import { ChangeDetectionStrategy, Component } from '@angular/core'
import { RouterLink } from '@angular/router'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { LogoComponent } from '@/shared/components/logo/logo.component'

@Component({
  selector: 'app-site-header',
  imports: [RouterLink, ButtonComponent, LogoComponent],
  templateUrl: './site-header.component.html',
  styleUrl: './site-header.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SiteHeaderComponent {}
