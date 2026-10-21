import { HttpErrorResponse } from '@angular/common/http'
import { computed, inject, Injectable, signal } from '@angular/core'
import { finalize, Observable, tap } from 'rxjs'
import {
  BookingOptions,
  CreateBookingRequest,
  CreateBookingResponse,
  ProblemDetails,
  Quote,
  QuoteRequest,
  VehicleCategory,
} from '@/core/models'
import { BookingApiService } from '@/core/services/booking-api.service'
import { problemMessage } from '@/core/utils/problem'

/** trip: choosing route and car; details: options and contact; done: going to payment. */
export type BookingStep = 'trip' | 'details' | 'done'

/** State of one booking flow; provided by the booking page. */
@Injectable()
export class BookingStore {
  private readonly api = inject(BookingApiService)

  readonly step = signal<BookingStep>('trip')
  readonly options = signal<BookingOptions | null>(null)
  readonly quote = signal<Quote | null>(null)
  readonly category = signal<VehicleCategory | null>(null)
  readonly result = signal<CreateBookingResponse | null>(null)
  readonly loading = signal(false)
  readonly error = signal<string | null>(null)
  /** Set when the price changed between the quote and the booking. */
  readonly priceChanged = signal(false)

  readonly vehicle = computed(() => {
    const quote = this.quote()
    const category = this.category()
    return quote?.vehicles.find(v => v.category === category) ?? null
  })

  loadOptions(): void {
    this.api.options().subscribe({ next: o => this.options.set(o) })
  }

  requestQuote(request: QuoteRequest): Observable<Quote> {
    this.loading.set(true)
    this.error.set(null)
    return this.api.quote(request).pipe(
      tap({
        next: quote => {
          this.quote.set(quote)
          // Keep the chosen vehicle only if it still fits.
          const chosen = quote.vehicles.find(
            v => v.category === this.category()
          )
          if (!chosen?.available) {
            this.category.set(null)
          }
        },
        error: err => this.error.set(problemMessage(err)),
      }),
      finalize(() => this.loading.set(false))
    )
  }

  book(request: CreateBookingRequest): Observable<CreateBookingResponse> {
    this.loading.set(true)
    this.error.set(null)
    this.priceChanged.set(false)
    return this.api.create(request).pipe(
      tap({
        next: result => {
          this.result.set(result)
          this.step.set('done')
        },
        error: (err: unknown) => {
          if (err instanceof HttpErrorResponse && err.status === 409) {
            const problem = err.error as ProblemDetails & { data?: Quote }
            if (problem.data) {
              this.quote.set(problem.data)
            }
            this.priceChanged.set(true)
          }
          this.error.set(problemMessage(err))
        },
      }),
      finalize(() => this.loading.set(false))
    )
  }

  clearQuote(): void {
    this.quote.set(null)
    this.category.set(null)
  }
}
