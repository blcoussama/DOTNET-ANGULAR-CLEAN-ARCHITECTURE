import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE } from '../http/api.config';
import { SimulationRequest, SimulationResult } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class SimulationApi {
  private readonly http = inject(HttpClient);

  /** Simulation publique ; le revenu et les charges sont facultatifs (calcul de l'endettement). */
  simulate(request: SimulationRequest): Observable<SimulationResult> {
    const body: SimulationRequest = {
      productType: request.productType,
      amount: request.amount,
      durationMonths: request.durationMonths,
    };
    if (
      request.monthlyIncome !== undefined &&
      request.monthlyIncome !== null &&
      request.monthlyIncome > 0
    ) {
      body.monthlyIncome = request.monthlyIncome;
      body.monthlyCharges = request.monthlyCharges ?? 0;
    }
    return this.http.post<SimulationResult>(`${API_BASE}/simulations`, body);
  }
}
