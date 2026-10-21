import { ChangeDetectionStrategy, Component, input } from '@angular/core'

@Component({
  selector: 'app-section-heading',
  templateUrl: './section-heading.component.html',
  styleUrl: './section-heading.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SectionHeadingComponent {
  readonly eyebrow = input<string>()
  readonly title = input.required<string>()
  readonly align = input<'center' | 'start'>('center')
}
