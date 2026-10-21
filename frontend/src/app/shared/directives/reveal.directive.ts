import {
  afterNextRender,
  DestroyRef,
  Directive,
  ElementRef,
  inject,
  input,
} from '@angular/core'

/**
 * Fades and lifts the element in once it scrolls into view. Only opacity and transform change, so the
 * browser animates it on the compositor (no layout work, no jank). Styles: .reveal in styles/_motion.scss.
 */
@Directive({
  selector: '[appReveal]',
  host: {
    class: 'reveal',
    '[style.--reveal-delay]': "delay() + 'ms'",
    '[attr.data-reveal]': 'appReveal() || null',
  },
})
export class RevealDirective {
  /** Variant: "" (fade up), "left", "right", "zoom". */
  readonly appReveal = input<'' | 'left' | 'right' | 'zoom'>('')
  /** Stagger, in milliseconds. */
  readonly delay = input(0)

  constructor() {
    const element = inject(ElementRef<HTMLElement>).nativeElement
    const destroyRef = inject(DestroyRef)
    afterNextRender(() => {
      if (typeof IntersectionObserver === 'undefined') {
        element.classList.add('reveal--in')
        return
      }
      const observer = new IntersectionObserver(
        entries => {
          if (entries.some(e => e.isIntersecting)) {
            element.classList.add('reveal--in')
            observer.disconnect()
          }
        },
        { rootMargin: '0px 0px -10% 0px', threshold: 0.15 }
      )
      observer.observe(element)
      destroyRef.onDestroy(() => observer.disconnect())
    })
  }
}
