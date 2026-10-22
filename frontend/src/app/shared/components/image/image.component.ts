import { DOCUMENT, isPlatformServer } from '@angular/common'
import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  input,
  OnInit,
  PLATFORM_ID,
  signal,
  viewChild,
} from '@angular/core'
import { ImageAsset } from '@/core/models'

/**
 * An image that never pops in: a tiny blurred preview (or a shimmer when there is none) fills the
 * box straight away, and the real image fades in once it has loaded. The box keeps the image's
 * proportions, so nothing moves while it loads.
 *
 * `fit="cover"` fills the parent instead (banners, backgrounds); `priority` loads it first and,
 * on the prerendered page, preloads it.
 */
@Component({
  selector: 'app-image',
  templateUrl: './image.component.html',
  styleUrl: './image.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'image',
    '[class.image--cover]': "fit() === 'cover'",
    '[class.image--loaded]': 'loaded()',
    '[class.image--failed]': 'failed()',
    '[style.aspect-ratio]': "fit() === 'cover' ? null : ratio()",
    '[style.--placeholder]': 'placeholderUrl()',
  },
})
export class ImageComponent implements OnInit {
  /** A prepared photo (IMAGES.hero) or a plain URL (a partner's logo). */
  readonly image = input.required<ImageAsset | string>()
  readonly alt = input.required<string>()
  /** Which width the image is shown at, for picking from srcset. */
  readonly sizes = input('100vw')
  readonly fit = input<'contain' | 'cover'>('contain')
  readonly priority = input(false)

  protected readonly asset = computed<ImageAsset | null>(() => {
    const image = this.image()
    return typeof image === 'string' ? null : image
  })
  protected readonly src = computed(() => {
    const image = this.image()
    return typeof image === 'string' ? image : image.src
  })
  protected readonly ratio = computed(() => {
    const asset = this.asset()
    return asset ? `${asset.width} / ${asset.height}` : null
  })
  protected readonly placeholderUrl = computed(() => {
    const placeholder = this.asset()?.placeholder
    return placeholder ? `url("${placeholder}")` : null
  })

  protected readonly loaded = signal(false)
  protected readonly failed = signal(false)
  private readonly img = viewChild.required<ElementRef<HTMLImageElement>>('img')
  private readonly document = inject(DOCUMENT)
  private readonly server = isPlatformServer(inject(PLATFORM_ID))

  constructor() {
    // Already loaded (cache, or before the app started): no load event will come.
    afterNextRender(() => {
      const img = this.img().nativeElement
      if (img.complete && img.naturalWidth > 0) {
        this.loaded.set(true)
      }
    })
  }

  ngOnInit(): void {
    // The prerendered page asks for the banner before the scripts have even loaded.
    if (this.server && this.priority()) {
      this.preload()
    }
  }

  protected onLoad(): void {
    this.loaded.set(true)
  }

  protected onError(): void {
    this.failed.set(true)
  }

  private preload(): void {
    const document = this.document
    const link = document.createElement('link')
    link.rel = 'preload'
    link.setAttribute('as', 'image')
    link.setAttribute('href', this.src())
    const srcset = this.asset()?.srcset
    if (srcset) {
      link.setAttribute('imagesrcset', srcset)
      link.setAttribute('imagesizes', this.sizes())
    }
    link.setAttribute('fetchpriority', 'high')
    document.head.appendChild(link)
  }
}
