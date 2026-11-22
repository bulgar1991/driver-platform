import { HttpClient, HttpParams } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { Observable } from 'rxjs'
import { MessageList, MessageQuery } from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

/** Admin → Messages: the WhatsApp and emails sent to drivers and clients. */
@Injectable({ providedIn: 'root' })
export class AdminMessagesService {
  private readonly http = inject(HttpClient)
  private readonly url = `${inject(API_BASE_URL)}/admin/messages`

  list(query: MessageQuery): Observable<MessageList> {
    let params = new HttpParams()
    for (const [key, value] of Object.entries(query)) {
      if (value !== null && value !== '' && value !== 'Any') {
        params = params.set(key, String(value))
      }
    }
    return this.http.get<MessageList>(this.url, { params })
  }

  resend(id: number): Observable<void> {
    return this.http.post<void>(`${this.url}/${id}/resend`, null)
  }
}
