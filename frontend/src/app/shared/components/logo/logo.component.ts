import { ChangeDetectionStrategy, Component, input } from '@angular/core'
import { BRAND } from '@/core/brand'

/** The brand's name in capitals, with its accent word in gold (src/brands/<client>/brand.json). */
@Component({
  selector: 'app-logo',
  templateUrl: './logo.component.html',
  styleUrl: './logo.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LogoComponent {
  readonly size = input<'sm' | 'lg'>('sm')

  protected readonly parts = splitLogo(BRAND.logo.text, BRAND.logo.accent)
}

export function splitLogo(
  text: string,
  accent: string
): { before: string; accent: string; after: string } {
  const at = accent ? text.indexOf(accent) : -1
  return at < 0
    ? { before: text, accent: '', after: '' }
    : {
        before: text.slice(0, at),
        accent,
        after: text.slice(at + accent.length),
      }
}
