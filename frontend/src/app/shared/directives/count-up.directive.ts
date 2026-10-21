import {
  afterNextRender,
  DestroyRef,
  Directive,
  ElementRef,
  inject,
  input,
} from '@angular/core'

/** Counts from 0 to the value when the element first becomes visible. Respects reduced motion. */
@Directive({ selector: '[appCountUp]' })
export class CountUpDirective {
  readonly appCountUp = input.required<number>()
  readonly duration = input(1600)

  constructor() {
    const element = inject(ElementRef<HTMLElement>).nativeElement
    const destroyRef = inject(DestroyRef)
    let frame = 0

    afterNextRender(() => {
      const target = this.appCountUp()
      const reduced = window.matchMedia?.(
        '(prefers-reduced-motion: reduce)'
      ).matches
      if (reduced || typeof IntersectionObserver === 'undefined') {
        element.textContent = String(target)
        return
      }
      element.textContent = '0'
      const observer = new IntersectionObserver(entries => {
        if (!entries.some(e => e.isIntersecting)) {
          return
        }
        observer.disconnect()
        const start = performance.now()
        const tick = (now: number) => {
          const t = Math.min(1, (now - start) / this.duration())
          // easeOutCubic
          element.textContent = String(
            Math.round(target * (1 - Math.pow(1 - t, 3)))
          )
          if (t < 1) {
            frame = requestAnimationFrame(tick)
          }
        }
        frame = requestAnimationFrame(tick)
      })
      observer.observe(element)
      destroyRef.onDestroy(() => {
        observer.disconnect()
        cancelAnimationFrame(frame)
      })
    })
  }
}
