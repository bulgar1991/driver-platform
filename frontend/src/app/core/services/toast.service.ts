import { inject, Injectable } from '@angular/core'
import { MatSnackBar } from '@angular/material/snack-bar'

export type ToastKind = 'success' | 'info' | 'error'

/**
 * Short messages that come and go (Angular Material snack bar): "Saved", "Link copied"…
 * Errors that the user has to fix stay next to their form; this is for confirmations.
 */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly snackBar = inject(MatSnackBar)

  success(message: string): void {
    this.show(message, 'success')
  }

  info(message: string): void {
    this.show(message, 'info')
  }

  error(message: string): void {
    this.show(message, 'error', 7000)
  }

  private show(message: string, kind: ToastKind, duration = 4000): void {
    this.snackBar.open(message, $localize`:Close a message:OK`, {
      duration,
      horizontalPosition: 'end',
      verticalPosition: 'bottom',
      panelClass: ['toast', `toast--${kind}`],
      politeness: kind === 'error' ? 'assertive' : 'polite',
    })
  }
}
