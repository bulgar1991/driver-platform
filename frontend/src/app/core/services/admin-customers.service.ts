import { HttpClient, HttpParams } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { Observable } from 'rxjs'
import {
  CustomerDetail,
  CustomerList,
  CustomerListItem,
  CustomerQuery,
  CustomerRequest,
} from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

/** Admin → Customers: the people who booked, their rides and their card. */
@Injectable({ providedIn: 'root' })
export class AdminCustomersService {
  private readonly http = inject(HttpClient)
  private readonly url = `${inject(API_BASE_URL)}/admin/customers`

  list(query: CustomerQuery): Observable<CustomerList> {
    return this.http.get<CustomerList>(this.url, { params: params(query) })
  }

  /** Every customer matching the filters, for the Excel export. */
  export(query: CustomerQuery): Observable<CustomerListItem[]> {
    return this.http.get<CustomerListItem[]>(`${this.url}/export`, {
      params: params(query),
    })
  }

  get(id: string): Observable<CustomerDetail> {
    return this.http.get<CustomerDetail>(`${this.url}/${id}`)
  }

  update(id: string, request: CustomerRequest): Observable<CustomerDetail> {
    return this.http.put<CustomerDetail>(`${this.url}/${id}`, request)
  }
}

function params(query: CustomerQuery): HttpParams {
  let p = new HttpParams()
    .set('filter', query.filter)
    .set('sort', query.sort)
    .set('page', query.page)
    .set('pageSize', query.pageSize)
  if (query.search.trim()) p = p.set('search', query.search.trim())
  return p
}
