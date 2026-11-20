import { HttpClient } from '@angular/common/http'
import { inject, Injectable } from '@angular/core'
import { Observable } from 'rxjs'
import {
  ExtraFee,
  ExtraType,
  PricingOverview,
  PricingSettings,
  SurchargeRule,
  SurchargeRuleRequest,
  VehicleCategory,
  VehicleRate,
  VehicleRateRequest,
  DeleteVehicleRateResult,
} from '../models'
import { API_BASE_URL } from '../tokens/api-base-url.token'

@Injectable({ providedIn: 'root' })
export class AdminPricingService {
  private readonly http = inject(HttpClient)
  private readonly url = `${inject(API_BASE_URL)}/admin/pricing`

  get(): Observable<PricingOverview> {
    return this.http.get<PricingOverview>(this.url)
  }

  updateRate(
    category: VehicleCategory,
    rate: VehicleRateRequest
  ): Observable<VehicleRate> {
    return this.http.put<VehicleRate>(`${this.url}/rates/${category}`, rate)
  }

  /** A new tariff; its code is made from the English name. */
  createRate(rate: VehicleRateRequest): Observable<VehicleRate> {
    return this.http.post<VehicleRate>(`${this.url}/rates`, rate)
  }

  /** Deleted when unused; hidden when bookings or cars use it. */
  deleteRate(category: VehicleCategory): Observable<DeleteVehicleRateResult> {
    return this.http.delete<DeleteVehicleRateResult>(
      `${this.url}/rates/${category}`
    )
  }

  setRateActive(
    category: VehicleCategory,
    isActive: boolean
  ): Observable<VehicleRate> {
    return this.http.put<VehicleRate>(`${this.url}/rates/${category}/active`, {
      isActive,
    })
  }

  updateExtra(type: ExtraType, amount: number): Observable<ExtraFee> {
    return this.http.put<ExtraFee>(`${this.url}/extras/${type}`, { amount })
  }

  updateSettings(settings: PricingSettings): Observable<PricingSettings> {
    return this.http.put<PricingSettings>(`${this.url}/settings`, settings)
  }

  createSurcharge(rule: SurchargeRuleRequest): Observable<SurchargeRule> {
    return this.http.post<SurchargeRule>(`${this.url}/surcharges`, rule)
  }

  updateSurcharge(
    id: string,
    rule: SurchargeRuleRequest
  ): Observable<SurchargeRule> {
    return this.http.put<SurchargeRule>(`${this.url}/surcharges/${id}`, rule)
  }

  deleteSurcharge(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/surcharges/${id}`)
  }
}
