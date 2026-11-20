import { HttpClient } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { Observable, tap } from 'rxjs'
import { ContentText, SiteContent } from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'
import { SiteContentService } from './site-content.service'

/** Admin → Website content. Every change also updates what this browser shows of the site. */
@Injectable({ providedIn: 'root' })
export class AdminContentService {
  private readonly http = inject(HttpClient)
  private readonly url = `${inject(API_BASE_URL)}/admin/content`
  private readonly site = inject(SiteContentService)

  get(): Observable<SiteContent> {
    return this.http.get<SiteContent>(this.url).pipe(tap(c => this.site.set(c)))
  }

  /** Empty languages go back to the built-in text. */
  setText(key: string, text: ContentText): Observable<SiteContent> {
    return this.http
      .put<SiteContent>(`${this.url}/texts/${key}`, text)
      .pipe(tap(c => this.site.set(c)))
  }

  /** Stored as WebP by the server (at most 700 KB). */
  setImage(key: string, file: Blob): Observable<SiteContent> {
    const form = new FormData()
    form.append('file', file)
    return this.http
      .put<SiteContent>(`${this.url}/images/${key}`, form)
      .pipe(tap(c => this.site.set(c)))
  }

  removeImage(key: string): Observable<SiteContent> {
    return this.http
      .delete<SiteContent>(`${this.url}/images/${key}`)
      .pipe(tap(c => this.site.set(c)))
  }
}
