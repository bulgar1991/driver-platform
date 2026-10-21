import { HttpClient } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { Observable } from 'rxjs'
import {
  DriverApplication,
  DriverApplicationRequest,
  DriverApplicationStatus,
  Testimonial,
} from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

/** Public home page content, and the driver applications it collects. */
@Injectable({ providedIn: 'root' })
export class SiteService {
  private readonly http = inject(HttpClient)
  private readonly apiUrl = inject(API_BASE_URL)

  testimonials(): Observable<Testimonial[]> {
    return this.http.get<Testimonial[]>(`${this.apiUrl}/testimonials`)
  }

  applyAsDriver(request: DriverApplicationRequest): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/driver-applications`, request)
  }

  driverApplications(
    status?: DriverApplicationStatus | null
  ): Observable<DriverApplication[]> {
    const query = status ? `?status=${status}` : ''
    return this.http.get<DriverApplication[]>(
      `${this.apiUrl}/admin/driver-applications${query}`
    )
  }

  setApplicationStatus(
    id: string,
    status: DriverApplicationStatus
  ): Observable<DriverApplication> {
    return this.http.put<DriverApplication>(
      `${this.apiUrl}/admin/driver-applications/${id}/status`,
      { status }
    )
  }
}
