import { DatePipe } from '@angular/common'
import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  signal,
  input,
} from '@angular/core'
import { toSignal } from '@angular/core/rxjs-interop'
import { RouterLink } from '@angular/router'
import { catchError, of } from 'rxjs'
import { VehicleCategory } from '@/core/models'
import { SiteService } from '@/core/services/site.service'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { SectionHeadingComponent } from '@/shared/components/section-heading/section-heading.component'
import { RevealDirective } from '@/shared/directives/reveal.directive'
import { VEHICLE_TEXT } from '../../home.content'

const AUTOPLAY_MS = 6000
const SWIPE_PX = 50

/**
 * Happy clients: a swiper of real, consented reviews (no library).
 * Slides move with transform only; autoplay runs while visible and not hovered, and never with
 * reduced motion. Swipe, arrow buttons, keyboard arrows and dots all work.
 */
@Component({
  selector: 'app-testimonials',
  imports: [
    DatePipe,
    RouterLink,
    ButtonComponent,
    IconComponent,
    SectionHeadingComponent,
    RevealDirective,
  ],
  templateUrl: './testimonials.component.html',
  styleUrl: './testimonials.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(keydown.arrowLeft)': 'go(-1)', '(keydown.arrowRight)': 'go(1)' },
})
export class TestimonialsComponent {
  /** The section is the whole page. */
  readonly page = input(false)
  protected readonly stars = [1, 2, 3, 4, 5]
  protected readonly labels = {
    position: (n: number, total: number) =>
      $localize`${n}:position: of ${total}:total:`,
    rating: (stars: number) => $localize`${stars}:stars: out of 5`,
    show: (n: number) => $localize`Show review ${n}:number:`,
  }

  protected vehicleName(category: string): string {
    return VEHICLE_TEXT[category as VehicleCategory]?.title ?? category
  }
  protected readonly items = toSignal(
    inject(SiteService)
      .testimonials()
      .pipe(catchError(() => of([]))),
    {
      initialValue: null,
    }
  )

  protected readonly perView = signal(1)
  protected readonly index = signal(0)
  protected readonly dragOffset = signal(0)
  protected readonly dragging = signal(false)

  protected readonly maxIndex = computed(() =>
    Math.max(0, (this.items()?.length ?? 0) - this.perView())
  )
  protected readonly pages = computed(() =>
    Array.from({ length: this.maxIndex() + 1 }, (_, i) => i)
  )
  protected readonly transform = computed(
    () =>
      `translate3d(calc(${-this.index() * (100 / this.perView())}% + ${this.dragOffset()}px), 0, 0)`
  )

  private paused = false
  private visible = false
  private startX = 0
  private timer?: ReturnType<typeof setInterval>

  constructor() {
    const host = inject(ElementRef<HTMLElement>).nativeElement
    const destroyRef = inject(DestroyRef)

    afterNextRender(() => {
      const reduced = window.matchMedia?.(
        '(prefers-reduced-motion: reduce)'
      ).matches
      const resize = () =>
        this.perView.set(
          window.innerWidth >= 1100 ? 3 : window.innerWidth >= 720 ? 2 : 1
        )
      resize()
      window.addEventListener('resize', resize, { passive: true })

      const observer =
        typeof IntersectionObserver === 'undefined'
          ? null
          : new IntersectionObserver(
              entries => (this.visible = entries.some(e => e.isIntersecting)),
              { threshold: 0.3 }
            )
      observer?.observe(host)

      if (!reduced) {
        this.timer = setInterval(() => {
          if (
            this.visible &&
            !this.paused &&
            !this.dragging() &&
            !document.hidden
          ) {
            this.index.set(
              this.index() >= this.maxIndex() ? 0 : this.index() + 1
            )
          }
        }, AUTOPLAY_MS)
      }

      destroyRef.onDestroy(() => {
        window.removeEventListener('resize', resize)
        observer?.disconnect()
        clearInterval(this.timer)
      })
    })
  }

  protected go(step: number): void {
    const next = this.index() + step
    this.index.set(
      next < 0 ? this.maxIndex() : next > this.maxIndex() ? 0 : next
    )
  }

  protected pause(paused: boolean): void {
    this.paused = paused
  }

  protected pointerDown(event: PointerEvent): void {
    this.startX = event.clientX
    this.dragging.set(true)
    ;(event.currentTarget as HTMLElement).setPointerCapture(event.pointerId)
  }

  protected pointerMove(event: PointerEvent): void {
    if (this.dragging()) {
      this.dragOffset.set(event.clientX - this.startX)
    }
  }

  protected pointerUp(): void {
    if (!this.dragging()) {
      return
    }
    const offset = this.dragOffset()
    this.dragging.set(false)
    this.dragOffset.set(0)
    if (Math.abs(offset) > SWIPE_PX) {
      this.go(offset < 0 ? 1 : -1)
    }
  }

  protected slideWidth(): string {
    return `${100 / this.perView()}%`
  }
}
