import { ChangeDetectionStrategy, Component, input } from '@angular/core'
import { BRAND } from '@/core/brand'

/**
 * The brand's name in capitals, with its accent word in gold, and for two-line logos a small gold
 * subline between hairlines (src/brands/<client>/brand.json).
 */
@Component({
  selector: 'app-logo',
  templateUrl: './logo.component.html',
  styleUrl: './logo.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LogoComponent {
  readonly size = input<'sm' | 'lg'>('sm')

  protected readonly parts = splitLogo(BRAND.logo.text, BRAND.logo.accent)
  /** Second line of a two-line logo (e.g. "VIP TRANSFER" under "HELVETIC"). */
  protected readonly subline = BRAND.logo.subline ?? ''
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
