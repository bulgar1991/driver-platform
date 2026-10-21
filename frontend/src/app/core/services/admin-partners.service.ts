import { HttpClient, HttpParams } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { Observable } from 'rxjs'
import {
  Partner,
  PartnerApiKey,
  PartnerRequest,
  WebhookDelivery,
} from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

@Injectable({ providedIn: 'root' })
export class AdminPartnersService {
  private readonly http = inject(HttpClient)
  private readonly url = `${inject(API_BASE_URL)}/admin/partners`
  /** Public OpenAPI description to share with partners' developers. */
  readonly docsUrl = `${inject(API_BASE_URL)}/partner/openapi/partner.json`

  list(): Observable<Partner[]> {
    return this.http.get<Partner[]>(this.url)
  }

  save(request: PartnerRequest, id?: string): Observable<Partner> {
    return id
      ? this.http.put<Partner>(`${this.url}/${id}`, request)
      : this.http.post<Partner>(this.url, request)
  }

  createKey(id: string): Observable<PartnerApiKey> {
    return this.http.post<PartnerApiKey>(`${this.url}/${id}/api-key`, null)
  }

  revokeKey(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}/api-key`)
  }

  webhookSecret(id: string, rotate = false): Observable<{ secret: string }> {
    return rotate
      ? this.http.post<{ secret: string }>(
          `${this.url}/${id}/webhook-secret`,
          null
        )
      : this.http.get<{ secret: string }>(`${this.url}/${id}/webhook-secret`)
  }

  deliveries(id: string): Observable<WebhookDelivery[]> {
    return this.http.get<WebhookDelivery[]>(`${this.url}/${id}/webhooks`)
  }

  statement(id: string, year: number, month: number): Observable<Blob> {
    const params = new HttpParams().set('year', year).set('month', month)
    return this.http.get(`${this.url}/${id}/statement`, {
      params,
      responseType: 'blob',
    })
  }
}
