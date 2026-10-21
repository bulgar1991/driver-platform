import { HttpClient, HttpParams } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { Observable } from 'rxjs'
import { PaymentListItem, PaymentStatus, RefundRequest } from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

@Injectable({ providedIn: 'root' })
export class AdminPaymentsService {
  private readonly http = inject(HttpClient)
  private readonly url = `${inject(API_BASE_URL)}/admin`

  list(status: PaymentStatus | null): Observable<PaymentListItem[]> {
    let params = new HttpParams().set('take', 200)
    if (status) {
      params = params.set('status', status)
    }
    return this.http.get<PaymentListItem[]>(`${this.url}/payments`, { params })
  }

  refund(id: string, request: RefundRequest): Observable<PaymentListItem> {
    return this.http.post<PaymentListItem>(
      `${this.url}/payments/${id}/refunds`,
      request
    )
  }

  /** Downloads go through HttpClient so the bearer token is sent. */
  invoicePdf(id: string): Observable<Blob> {
    return this.http.get(`${this.url}/invoices/${id}/pdf`, {
      responseType: 'blob',
    })
  }

  resend(id: string): Observable<void> {
    return this.http.post<void>(`${this.url}/invoices/${id}/resend`, null)
  }

  export(from: string, to: string): Observable<Blob> {
    const params = new HttpParams().set('from', from).set('to', to)
    return this.http.get(`${this.url}/invoices/export`, {
      params,
      responseType: 'blob',
    })
  }
}
