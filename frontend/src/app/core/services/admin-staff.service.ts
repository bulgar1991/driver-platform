import { HttpClient, HttpParams } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { Observable } from 'rxjs'
import { AuditPage, StaffMember, StaffRole, TemporaryPassword } from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

@Injectable({ providedIn: 'root' })
export class AdminStaffService {
  private readonly http = inject(HttpClient)
  private readonly url = `${inject(API_BASE_URL)}/admin`

  list(): Observable<StaffMember[]> {
    return this.http.get<StaffMember[]>(`${this.url}/staff`)
  }

  create(
    email: string,
    fullName: string,
    role: StaffRole
  ): Observable<TemporaryPassword> {
    return this.http.post<TemporaryPassword>(`${this.url}/staff`, {
      email,
      fullName,
      role,
    })
  }

  update(
    id: string,
    fullName: string,
    role: StaffRole,
    isActive: boolean
  ): Observable<StaffMember> {
    return this.http.put<StaffMember>(`${this.url}/staff/${id}`, {
      fullName,
      role,
      isActive,
    })
  }

  resetPassword(id: string): Observable<TemporaryPassword> {
    return this.http.post<TemporaryPassword>(
      `${this.url}/staff/${id}/reset-password`,
      null
    )
  }

  audit(search: string, before?: number | null): Observable<AuditPage> {
    let params = new HttpParams()
    if (search.trim()) {
      params = params.set('search', search.trim())
    }
    if (before) {
      params = params.set('before', before)
    }
    return this.http.get<AuditPage>(`${this.url}/audit`, { params })
  }
}
