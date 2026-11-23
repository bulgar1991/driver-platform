import { HttpClient, HttpParams } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { Observable } from 'rxjs'
import { StatementDetail, StatementMonth } from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

/** Admin → Statements: each partner's month, issued, emailed and marked paid. */
@Injectable({ providedIn: 'root' })
export class AdminStatementsService {
  private readonly http = inject(HttpClient)
  private readonly url = `${inject(API_BASE_URL)}/admin/statements`

  month(year: number, month: number): Observable<StatementMonth> {
    const params = new HttpParams().set('year', year).set('month', month)
    return this.http.get<StatementMonth>(this.url, { params })
  }

  get(
    partnerId: string,
    year: number,
    month: number
  ): Observable<StatementDetail> {
    return this.http.get<StatementDetail>(this.path(partnerId, year, month))
  }

  issue(
    partnerId: string,
    year: number,
    month: number
  ): Observable<StatementDetail> {
    return this.http.post<StatementDetail>(
      `${this.path(partnerId, year, month)}/issue`,
      null
    )
  }

  send(
    partnerId: string,
    year: number,
    month: number,
    to: string | null
  ): Observable<StatementDetail> {
    return this.http.post<StatementDetail>(
      `${this.path(partnerId, year, month)}/send`,
      { to }
    )
  }

  setPaid(
    partnerId: string,
    year: number,
    month: number,
    paid: boolean,
    on: string | null,
    note: string | null
  ): Observable<StatementDetail> {
    return this.http.post<StatementDetail>(
      `${this.path(partnerId, year, month)}/paid`,
      { paid, on, note }
    )
  }

  file(
    partnerId: string,
    year: number,
    month: number,
    kind: 'pdf' | 'csv'
  ): Observable<Blob> {
    return this.http.get(`${this.path(partnerId, year, month)}/${kind}`, {
      responseType: 'blob',
    })
  }

  private path(partnerId: string, year: number, month: number): string {
    return `${this.url}/${partnerId}/${year}/${month}`
  }
}
