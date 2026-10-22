import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core'
import { FormsModule } from '@angular/forms'
import { Meta } from '@angular/platform-browser'
import { finalize } from 'rxjs'
import { ReviewPage } from '@/core/models'
import { DriverPortalService } from '@/core/services/driver-portal.service'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'

/** /review/:token — the one-time link sent after drop-off (business plan §5 step 7). */
@Component({
  selector: 'app-review',
  imports: [DatePipe, FormsModule, ButtonComponent, IconComponent],
  templateUrl: './review.component.html',
  styleUrl: './review.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReviewComponent {
  protected readonly t = {
    stars: (n: number) =>
      n === 1 ? $localize`1 star` : $localize`${n}:count: stars`,
    sending: $localize`Sending…`,
    send: $localize`Send review`,
  }
  readonly token = input.required<string>()

  private readonly api = inject(DriverPortalService)
  protected readonly stars = [1, 2, 3, 4, 5]
  protected readonly page = signal<ReviewPage | null>(null)
  protected readonly rating = signal(0)
  protected readonly hover = signal(0)
  protected comment = ''
  protected publish = false
  protected readonly sending = signal(false)
  protected readonly done = signal(false)
  protected readonly error = signal<string | null>(null)
  protected readonly invalid = signal(false)

  constructor() {
    inject(Meta).updateTag({ name: 'robots', content: 'noindex, nofollow' })
    effect(() => {
      const token = this.token()
      untracked(() =>
        this.api.review(token).subscribe({
          next: page => {
            this.page.set(page)
            this.done.set(page.submitted)
          },
          error: err => {
            this.invalid.set((err as { status?: number }).status === 404)
            this.error.set(problemMessage(err))
          },
        })
      )
    })
  }

  protected submit(): void {
    if (!this.rating()) {
      this.error.set($localize`Choose from 1 to 5 stars.`)
      return
    }
    this.sending.set(true)
    this.error.set(null)
    this.api
      .submitReview(
        this.token(),
        this.rating(),
        this.comment.trim(),
        this.publish
      )
      .pipe(finalize(() => this.sending.set(false)))
      .subscribe({
        next: () => this.done.set(true),
        error: err => this.error.set(problemMessage(err)),
      })
  }
}
