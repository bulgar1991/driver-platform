import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core'
import { finalize } from 'rxjs'
import { DriverApplication, DriverApplicationStatus } from '@/core/models'
import { SiteService } from '@/core/services/site.service'
import { problemMessage } from '@/core/utils/problem'

const STATUSES: DriverApplicationStatus[] = [
  'New',
  'Contacted',
  'Accepted',
  'Rejected',
]

/** Applications from the "For drivers" form on the website. */
@Component({
  selector: 'app-applications',
  imports: [DatePipe],
  templateUrl: './applications.component.html',
  styleUrl: './applications.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ApplicationsComponent implements OnInit {
  private readonly site = inject(SiteService)
  protected readonly statuses = STATUSES
  protected readonly filter = signal<DriverApplicationStatus | null>('New')
  protected readonly items = signal<DriverApplication[]>([])
  protected readonly loading = signal(true)
  protected readonly error = signal<string | null>(null)

  ngOnInit(): void {
    this.load()
  }

  protected show(status: DriverApplicationStatus | null): void {
    this.filter.set(status)
    this.load()
  }

  protected setStatus(
    application: DriverApplication,
    status: DriverApplicationStatus
  ): void {
    this.site.setApplicationStatus(application.id, status).subscribe({
      next: updated =>
        this.items.update(list =>
          list.map(a => (a.id === updated.id ? updated : a))
        ),
      error: err => this.error.set(problemMessage(err)),
    })
  }

  private load(): void {
    this.loading.set(true)
    this.site
      .driverApplications(this.filter())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: list => this.items.set(list),
        error: err => this.error.set(problemMessage(err)),
      })
  }
}
