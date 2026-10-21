import { ChangeDetectionStrategy, Component, input } from '@angular/core'
import { RouterLink } from '@angular/router'

export type LegalPage = 'privacy' | 'terms'

/**
 * /privacy and /terms. DRAFTS: the structure follows GDPR art. 13 and the Italian consumer code, but
 * the text, the company data and every item marked [to define] must be reviewed by a lawyer before launch.
 */
@Component({
  selector: 'app-legal',
  imports: [RouterLink],
  templateUrl: './legal.component.html',
  styleUrl: './legal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LegalComponent {
  /** From the route data. */
  readonly page = input<LegalPage>('privacy')
}
