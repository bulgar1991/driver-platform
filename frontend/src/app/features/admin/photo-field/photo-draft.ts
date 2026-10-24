import { signal } from '@angular/core'

/** The picture in an open dialog, and what saving will do with it. */
export class PhotoDraft {
  /** undefined: keep the saved one; null: remove it; a Blob: upload it. */
  readonly change = signal<Blob | null | undefined>(undefined)
  /** What to show: the saved picture's URL or a local preview. */
  readonly src = signal<string | null>(null)
  private objectUrl: string | null = null

  /** A dialog opens: show the saved picture, nothing to send. */
  reset(savedUrl: string | null): void {
    this.revoke()
    this.change.set(undefined)
    this.src.set(savedUrl)
  }

  set(blob: Blob | null): void {
    this.revoke()
    if (blob) {
      this.objectUrl = URL.createObjectURL(blob)
    }
    this.change.set(blob)
    this.src.set(this.objectUrl)
  }

  private revoke(): void {
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl)
      this.objectUrl = null
    }
  }
}
