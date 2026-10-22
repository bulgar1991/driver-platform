import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core'
import { finalize } from 'rxjs'
import { Driver, Review } from '@/core/models'
import { AdminFleetService } from '@/core/services/admin-fleet.service'
import { problemMessage } from '@/core/utils/problem'
import { IconComponent } from '@/shared/components/icon/icon.component'

/** Client reviews of rides and drivers. */
@Component({
  selector: 'app-reviews',
  imports: [DatePipe, IconComponent],
  templateUrl: './reviews.component.html',
  styleUrl: './reviews.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReviewsComponent implements OnInit {
  protected readonly tx = {
    rating: (rating: number | null) =>
      rating ? $localize`${rating}:rating: of 5` : $localize`Not rated yet`,
  }

  private readonly api = inject(AdminFleetService)

  protected readonly stars = [1, 2, 3, 4, 5]
  protected readonly drivers = signal<Driver[]>([])
  protected readonly driverId = signal<string | null>(null)
  protected readonly reviews = signal<Review[]>([])
  protected readonly loading = signal(true)
  protected readonly error = signal<string | null>(null)

  protected readonly answered = computed(() =>
    this.reviews().filter(r => r.rating !== null)
  )
  protected readonly average = computed(() => {
    const rated = this.answered()
    return rated.length
      ? rated.reduce((sum, r) => sum + r.rating!, 0) / rated.length
      : null
  })

  ngOnInit(): void {
    this.api.drivers(true).subscribe({ next: d => this.drivers.set(d) })
    this.load()
  }

  protected filter(driverId: string): void {
    this.driverId.set(driverId || null)
    this.load()
  }

  private load(): void {
    this.loading.set(true)
    this.api
      .reviews(this.driverId())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: r => this.reviews.set(r),
        error: err => this.error.set(problemMessage(err)),
      })
  }
}
