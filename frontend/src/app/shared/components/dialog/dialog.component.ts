import { ChangeDetectionStrategy, Component, input } from '@angular/core'
import {
  MatDialogActions,
  MatDialogClose,
  MatDialogContent,
  MatDialogTitle,
} from '@angular/material/dialog'
import { IconComponent } from '../icon/icon.component'

/**
 * The frame of every popup: title, close button, scrolling body and (optional) buttons at the
 * bottom. Put it at the root of what DialogService (or `appDialogOpen`) opens:
 *
 *   <app-dialog [title]="…">
 *     …body…
 *     <div dialog-actions>…buttons…</div>
 *   </app-dialog>
 */
@Component({
  selector: 'app-dialog',
  imports: [
    MatDialogTitle,
    MatDialogContent,
    MatDialogActions,
    MatDialogClose,
    IconComponent,
  ],
  templateUrl: './dialog.component.html',
  styleUrl: './dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'app-dialog' },
})
export class DialogComponent {
  readonly title = input.required<string>()
  /** Hide the × (for a popup that must be answered with its own buttons). */
  readonly closable = input(true)
  protected readonly closeLabel = $localize`Close`
}
