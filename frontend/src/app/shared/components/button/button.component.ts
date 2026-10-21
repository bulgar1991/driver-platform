import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  ViewEncapsulation,
} from '@angular/core'

export type ButtonVariant = 'primary' | 'outline' | 'ghost'
export type ButtonSize = 'sm' | 'md'

/** Usage: <button appButton variant="outline">…</button> or <a appButton routerLink="…">. */
@Component({
  // Attribute selector keeps native <button>/<a> semantics.
  // eslint-disable-next-line @angular-eslint/component-selector
  selector: 'button[appButton], a[appButton]',
  template: '<ng-content />',
  styleUrl: './button.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  // The BEM block sits on the host element, so styles must not be scoped.
  encapsulation: ViewEncapsulation.None,
  host: { '[class]': 'classes()' },
})
export class ButtonComponent {
  readonly variant = input<ButtonVariant>('primary')
  readonly size = input<ButtonSize>('md')
  readonly block = input(false)

  protected readonly classes = computed(() =>
    [
      'button',
      `button--${this.variant()}`,
      `button--${this.size()}`,
      this.block() ? 'button--block' : '',
    ].join(' ')
  )
}
