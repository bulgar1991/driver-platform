import { ChangeDetectionStrategy, Component, inject } from '@angular/core'
import { MAT_DIALOG_DATA, MatDialogClose } from '@angular/material/dialog'
import { ButtonComponent } from '../button/button.component'
import { DialogComponent } from './dialog.component'

export interface ConfirmDialogData {
  title: string
  message: string
  /** Text of the button that goes ahead. */
  confirm: string
  /** Red button: the action deletes or undoes something. */
  danger?: boolean
}

/** "Are you sure?" — closes with true (go ahead) or nothing. Open it with DialogService.confirm. */
@Component({
  selector: 'app-confirm-dialog',
  imports: [DialogComponent, ButtonComponent, MatDialogClose],
  template: `
    <app-dialog [title]="data.title">
      <p class="confirm-dialog__message">{{ data.message }}</p>
      <div dialog-actions>
        <button appButton variant="ghost" type="button" mat-dialog-close>{{
          cancel
        }}</button>
        <button
          appButton
          type="button"
          [class.confirm-dialog__danger]="data.danger"
          [mat-dialog-close]="true">
          {{ data.confirm }}
        </button>
      </div>
    </app-dialog>
  `,
  styles: `
    .confirm-dialog__message {
      @apply leading-relaxed text-ivory-100;
    }
    .confirm-dialog__danger.button {
      @apply border-danger bg-danger bg-none text-ivory shadow-none hover:bg-danger/85;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmDialogComponent {
  protected readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA)
  protected readonly cancel = $localize`Cancel`
}
