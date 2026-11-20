import {
  ChangeDetectionStrategy,
  Component,
  input,
  signal,
} from '@angular/core'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { IMAGE_TYPES, imageUploadError } from '@/core/utils/image-upload'
import { PhotoDraft } from './photo-draft'

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
  protected readonly types = IMAGE_TYPES

  /** Sent as it is (up to 700 KB); the server turns it into WebP. */
  protected picked(event: Event): void {
    const field = event.target as HTMLInputElement
    const file = field.files?.[0]
    field.value = ''
    if (!file) {
      return
    }
    const error = imageUploadError(file)
    this.error.set(error)
    if (!error) {
      this.draft().set(file)
    }
  }

  protected remove(): void {
    this.error.set(null)
    this.draft().set(null)
  }
}
