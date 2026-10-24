import {
  ChangeDetectionStrategy,
  Component,
  input,
  signal,
} from '@angular/core'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { PhotoDraft } from './photo-draft'

/** Longest side after shrinking: sharp in the panel, a few hundred kB to upload. */
const MAX_SIDE = 1600

/** Choose, preview and remove a vehicle or driver picture. Saved with the rest of the form. */
@Component({
  selector: 'app-photo-field',
  imports: [ButtonComponent, IconComponent],
  templateUrl: './photo-field.component.html',
  styleUrl: './photo-field.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PhotoFieldComponent {
  readonly draft = input.required<PhotoDraft>()
  readonly alt = input.required<string>()
  /** Round for people, wide for cars. */
  readonly shape = input<'round' | 'wide'>('wide')

  protected readonly error = signal<string | null>(null)
  protected readonly busy = signal(false)

  protected async picked(event: Event): Promise<void> {
    const field = event.target as HTMLInputElement
    const file = field.files?.[0]
    field.value = ''
    if (!file) {
      return
    }
    this.error.set(null)
    this.busy.set(true)
    try {
      this.draft().set(await shrink(file))
    } catch {
      this.error.set(
        $localize`This picture can't be read. Choose a JPEG, PNG or WebP.`
      )
    } finally {
      this.busy.set(false)
    }
  }

  protected remove(): void {
    this.error.set(null)
    this.draft().set(null)
  }
}

/** Phone photos are often 5–10 MB: redraw them as a JPEG of at most {@link MAX_SIDE} pixels. */
async function shrink(file: File): Promise<Blob> {
  const image = await createImageBitmap(file)
  const scale = Math.min(1, MAX_SIDE / Math.max(image.width, image.height))
  const canvas = document.createElement('canvas')
  canvas.width = Math.round(image.width * scale)
  canvas.height = Math.round(image.height * scale)
  const context = canvas.getContext('2d')!
  // JPEG has no transparency: put PNG cut-outs on white rather than black.
  context.fillStyle = '#fff'
  context.fillRect(0, 0, canvas.width, canvas.height)
  context.drawImage(image, 0, 0, canvas.width, canvas.height)
  image.close()
  return new Promise((resolve, reject) =>
    canvas.toBlob(
      blob => (blob ? resolve(blob) : reject(new Error('toBlob'))),
      'image/jpeg',
      0.85
    )
  )
}
