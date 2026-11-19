import {
  DestroyRef,
  Directive,
  effect,
  inject,
  input,
  output,
  TemplateRef,
  untracked,
  ViewContainerRef,
} from '@angular/core'
import { MatDialogRef } from '@angular/material/dialog'
import { DialogService } from './dialog.service'

/**
 * A popup written in the page's own template, shown while a condition holds:
 *
 *   <ng-template [appDialogOpen]="editing() !== undefined" dialogWidth="44rem"
 *                (dialogClosed)="editing.set(undefined)">
 *     <app-dialog [title]="…">…</app-dialog>
 *   </ng-template>
 *
 * When the user closes it (×, Escape, click outside, Back), `dialogClosed` fires so the page can
 * reset its state; when the condition turns false (saved), the popup closes by itself.
 */
@Directive({ selector: 'ng-template[appDialogOpen]' })
export class DialogOpenDirective {
  readonly open = input.required<boolean>({ alias: 'appDialogOpen' })
  readonly dialogWidth = input<string>('32rem')
  readonly dialogDisableClose = input(false)
  readonly dialogClosed = output<void>()

  private readonly template = inject(TemplateRef)
  private readonly viewContainerRef = inject(ViewContainerRef)
  private readonly dialogs = inject(DialogService)
  private ref: MatDialogRef<unknown> | null = null

  constructor() {
    effect(() => {
      const open = this.open()
      untracked(() => (open ? this.show() : this.hide()))
    })
    inject(DestroyRef).onDestroy(() => this.hide())
  }

  private show(): void {
    if (this.ref) return
    const ref = this.dialogs.open(this.template, {
      width: this.dialogWidth(),
      disableClose: this.dialogDisableClose(),
      viewContainerRef: this.viewContainerRef,
    })
    this.ref = ref
    ref.afterClosed().subscribe(() => {
      // Closed by the user, not by the page: tell the page.
      if (this.ref === ref) {
        this.ref = null
        this.dialogClosed.emit()
      }
    })
  }

  private hide(): void {
    const ref = this.ref
    this.ref = null
    ref?.close()
  }
}
