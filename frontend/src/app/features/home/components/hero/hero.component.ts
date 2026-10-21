import { ChangeDetectionStrategy, Component } from '@angular/core'
import { RouterLink } from '@angular/router'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { TRUST_POINTS } from '../../home.content'

/** The banner: animated background, title and the main call to action. */
@Component({
  selector: 'app-hero',
  imports: [RouterLink, ButtonComponent, IconComponent],
  templateUrl: './hero.component.html',
  styleUrl: './hero.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HeroComponent {
  protected readonly trust = TRUST_POINTS
}
