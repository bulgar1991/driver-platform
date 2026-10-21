import { HttpClient } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { Observable } from 'rxjs'
import {
  BookingOptions,
  CreateBookingRequest,
  CreateBookingResponse,
  Quote,
  QuoteRequest,
} from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

@Injectable({ providedIn: 'root' })
export class BookingApiService {
  private readonly http = inject(HttpClient)
  private readonly apiUrl = inject(API_BASE_URL)

  options(): Observable<BookingOptions> {
    return this.http.get<BookingOptions>(`${this.apiUrl}/booking-options`)
  }

  /** Route, zones and the price of every vehicle; nothing is saved. */
  quote(request: QuoteRequest): Observable<Quote> {
    return this.http.post<Quote>(`${this.apiUrl}/quotes`, request)
  }

  /** 409 means the price changed; the new quote is in `error.data`. */
  create(request: CreateBookingRequest): Observable<CreateBookingResponse> {
    return this.http.post<CreateBookingResponse>(
      `${this.apiUrl}/bookings`,
      request
    )
  }
}
