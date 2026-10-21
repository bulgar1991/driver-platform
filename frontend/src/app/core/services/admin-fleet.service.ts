import { HttpClient, HttpParams } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { Observable } from 'rxjs'
import {
  Driver,
  DriverRequest,
  Review,
  Vehicle,
  VehicleRequest,
} from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

/** Drivers and vehicles. They are deactivated, never deleted. */
@Injectable({ providedIn: 'root' })
export class AdminFleetService {
  private readonly http = inject(HttpClient)
  private readonly url = `${inject(API_BASE_URL)}/admin`

  drivers(includeInactive = false): Observable<Driver[]> {
    const params = new HttpParams().set('includeInactive', includeInactive)
    return this.http.get<Driver[]>(`${this.url}/drivers`, { params })
  }

  saveDriver(request: DriverRequest, id?: string): Observable<Driver> {
    return id
      ? this.http.put<Driver>(`${this.url}/drivers/${id}`, request)
      : this.http.post<Driver>(`${this.url}/drivers`, request)
  }

  /** The driver's personal link (always the same until regenerated). */
  driverLink(id: string): Observable<{ url: string }> {
    return this.http.get<{ url: string }>(`${this.url}/drivers/${id}/link`)
  }

  /** New link; the old one stops working. */
  rotateDriverLink(id: string): Observable<{ url: string }> {
    return this.http.post<{ url: string }>(
      `${this.url}/drivers/${id}/link`,
      null
    )
  }

  reviews(driverId?: string | null): Observable<Review[]> {
    let params = new HttpParams()
    if (driverId) {
      params = params.set('driverId', driverId)
    }
    return this.http.get<Review[]>(`${this.url}/reviews`, { params })
  }

  vehicles(includeInactive = false): Observable<Vehicle[]> {
    const params = new HttpParams().set('includeInactive', includeInactive)
    return this.http.get<Vehicle[]>(`${this.url}/vehicles`, { params })
  }

  saveVehicle(request: VehicleRequest, id?: string): Observable<Vehicle> {
    return id
      ? this.http.put<Vehicle>(`${this.url}/vehicles/${id}`, request)
      : this.http.post<Vehicle>(`${this.url}/vehicles`, request)
  }
}
