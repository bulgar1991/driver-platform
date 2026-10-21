import { HttpClient } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { map, Observable } from 'rxjs'
import { DevPaymentSession, MyBooking, PaymentMethod } from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

/** The client's booking page and payment; authorised by the signed link, not by login. */
@Injectable({ providedIn: 'root' })
export class MyBookingService {
  private readonly http = inject(HttpClient)
  private readonly apiUrl = inject(API_BASE_URL)

  get(reference: string, token: string): Observable<MyBooking> {
    return this.http.get<MyBooking>(this.url(reference, token))
  }

  /** Opens a new payment page; resolves to its URL. */
  checkout(reference: string, token: string): Observable<string> {
    return this.http
      .post<{ url: string }>(`${this.url(reference, token)}/checkout`, null)
      .pipe(map(r => r.url))
  }

  invoiceUrl(reference: string, token: string, number: string): string {
    return `${this.url(reference, token)}/invoices/${encodeURIComponent(number)}`
  }

  devSession(sessionId: string): Observable<DevPaymentSession> {
    return this.http.get<DevPaymentSession>(this.devUrl(sessionId))
  }

  devComplete(sessionId: string, method: PaymentMethod): Observable<string> {
    return this.http
      .post<{ url: string }>(`${this.devUrl(sessionId)}/complete`, { method })
      .pipe(map(r => r.url))
  }

  devCancel(sessionId: string): Observable<string> {
    return this.http
      .post<{ url: string }>(`${this.devUrl(sessionId)}/cancel`, null)
      .pipe(map(r => r.url))
  }

  private url(reference: string, token: string): string {
    return `${this.apiUrl}/my-booking/${encodeURIComponent(reference)}/${encodeURIComponent(token)}`
  }

  private devUrl(sessionId: string): string {
    return `${this.apiUrl}/dev/payments/${encodeURIComponent(sessionId)}`
  }
}
