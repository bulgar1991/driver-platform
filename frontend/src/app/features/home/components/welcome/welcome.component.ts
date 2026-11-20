import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from '@angular/core'
import { IMAGES } from '@/core/images.generated'
import { SiteContentService } from '@/core/services/site-content.service'
import { ImageComponent } from '@/shared/components/image/image.component'
import { CountUpDirective } from '@/shared/directives/count-up.directive'
import { RevealDirective } from '@/shared/directives/reveal.directive'
import { HEADINGS } from '../../editable-content'
import { STATS, WELCOME } from '../../home.content'

@Component({
  selector: 'app-welcome',
  imports: [CountUpDirective, ImageComponent, RevealDirective],
  templateUrl: './welcome.component.html',
  styleUrl: './welcome.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WelcomeComponent {
  private readonly site = inject(SiteContentService)

  /** Texts and picture as changed in Admin → Website content, the built-in ones otherwise. */
  protected readonly content = computed(() => ({
    eyebrow: this.site.text('welcome.eyebrow', WELCOME.eyebrow),
    title: this.site.text('welcome.title', WELCOME.title),
    paragraphs: WELCOME.paragraphs.map((p, i) =>
      this.site.text(`welcome.paragraph.${i + 1}`, p)
    ),
    image: this.site.image('welcome.image', IMAGES.hero),
    alt: this.site.text('welcome.image-alt', HEADINGS.welcomeImageAlt),
  }))

  /** A figure counts up when it is a number ("5"); anything else ("24/7") is shown as it is. */
  protected readonly stats = computed(() =>
    STATS.map((s, i) => {
      const value = this.site.text(`stats.${i + 1}.value`, `${s.value}`)
      const number = /^\d+$/.test(value) ? Number(value) : null
      return {
        label: this.site.text(`stats.${i + 1}.label`, s.label),
        value,
        number,
        suffix: s.suffix ?? '',
      }
    })
  )
}
