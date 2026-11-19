import { ComponentType } from '@angular/cdk/portal'
import {
  inject,
  Injectable,
  TemplateRef,
  ViewContainerRef,
} from '@angular/core'
import { MatDialog, MatDialogRef } from '@angular/material/dialog'
import { map, Observable } from 'rxjs'
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from './confirm-dialog.component'

export interface AppDialogConfig<D = unknown> {
  data?: D
  /** CSS width, e.g. '30rem'. Never wider than the screen minus 1rem a side. */
  width?: string
  /** Only the dialog's own buttons close it (no Escape, no click outside). */
  disableClose?: boolean
  /** Where a template opened as a dialog takes its injector from. */
  viewContainerRef?: ViewContainerRef
}

/**
 * Opens popups (Angular Material dialog) the same way everywhere: same width rules, the page
 * behind stops scrolling, Escape / a click outside / the browser's Back button close it, and focus
 * goes back to where it was. Wrap the content in <app-dialog>.
 */
@Injectable({ providedIn: 'root' })
export class DialogService {
  private readonly dialog = inject(MatDialog)

  open<C, D = unknown, R = unknown>(
    content: ComponentType<C> | TemplateRef<C>,
    config: AppDialogConfig<D> = {}
  ): MatDialogRef<C, R> {
    return this.dialog.open<C, D, R>(content, {
      data: config.data,
      width: config.width ?? '32rem',
      maxWidth: 'calc(100vw - 2rem)',
      maxHeight: 'calc(100dvh - 2rem)',
      disableClose: config.disableClose ?? false,
      viewContainerRef: config.viewContainerRef,
      autoFocus: 'first-heading',
      restoreFocus: true,
      closeOnNavigation: true,
      panelClass: 'app-dialog-panel',
    })
  }

  /** "Are you sure?" popup: emits true when the user goes ahead, false otherwise. */
  confirm(data: ConfirmDialogData): Observable<boolean> {
    return this.open<ConfirmDialogComponent, ConfirmDialogData, boolean>(
      ConfirmDialogComponent,
      { data, width: '28rem' }
    )
      .afterClosed()
      .pipe(map(result => result === true))
  }

  closeAll(): void {
    this.dialog.closeAll()
  }
}
