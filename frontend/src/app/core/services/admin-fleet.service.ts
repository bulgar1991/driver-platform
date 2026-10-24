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

/** Drivers and vehicles. Only those never used on a shift can be deleted; the others are deactivated. */
@Injectable({ providedIn: 'root' })
export class AdminFleetService {
  private readonly http = inject(HttpClient)
  private readonly api = inject(API_BASE_URL)
  private readonly url = `${this.api}/admin`

  /** Pictures are public by id, so a plain <img> can show them. */
  photoUrl(photoId: string | null): string | null {
    return photoId ? `${this.api}/photos/${photoId}` : null
  }

  drivers(includeInactive = false): Observable<Driver[]> {
    const params = new HttpParams().set('includeInactive', includeInactive)
    return this.http.get<Driver[]>(`${this.url}/drivers`, { params })
  }

  saveDriver(request: DriverRequest, id?: string): Observable<Driver> {
    return id
      ? this.http.put<Driver>(`${this.url}/drivers/${id}`, request)
      : this.http.post<Driver>(`${this.url}/drivers`, request)
  }

  deleteDriver(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/drivers/${id}`)
  }

  /** Uploads a new picture, or removes it when `photo` is null. */
  setDriverPhoto(id: string, photo: Blob | null): Observable<Driver> {
    return photo
      ? this.http.put<Driver>(`${this.url}/drivers/${id}/photo`, form(photo))
      : this.http.delete<Driver>(`${this.url}/drivers/${id}/photo`)
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

  deleteVehicle(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/vehicles/${id}`)
  }

  /** Uploads a new picture, or removes it when `photo` is null. */
  setVehiclePhoto(id: string, photo: Blob | null): Observable<Vehicle> {
    return photo
      ? this.http.put<Vehicle>(`${this.url}/vehicles/${id}/photo`, form(photo))
      : this.http.delete<Vehicle>(`${this.url}/vehicles/${id}/photo`)
  }
}

function form(photo: Blob): FormData {
  const data = new FormData()
  data.append('file', photo, 'photo.jpg')
  return data
}
