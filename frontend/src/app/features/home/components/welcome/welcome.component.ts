import { ChangeDetectionStrategy, Component } from '@angular/core'
import { IMAGES } from '@/core/images.generated'
import { ImageComponent } from '@/shared/components/image/image.component'
import { CountUpDirective } from '@/shared/directives/count-up.directive'
import { RevealDirective } from '@/shared/directives/reveal.directive'
import { STATS, WELCOME } from '../../home.content'

@Component({
  selector: 'app-welcome',
  imports: [CountUpDirective, ImageComponent, RevealDirective],
  templateUrl: './welcome.component.html',
  styleUrl: './welcome.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WelcomeComponent {
  protected readonly content = WELCOME
  protected readonly stats = STATS
  protected readonly images = IMAGES
}
