import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CreateMovementRequest,
  Equipment,
  Movement,
  MovementStatus,
  ShiftKind,
  Site,
  UpdateMovementRequest
} from './models';

@Injectable({ providedIn: 'root' })
export class DispatchApiService {
  private readonly http = inject(HttpClient);

  getSites(): Observable<Site[]> {
    return this.http.get<Site[]>('/api/sites');
  }

  getEquipment(siteId: string): Observable<Equipment[]> {
    return this.http.get<Equipment[]>(`/api/sites/${encodeURIComponent(siteId)}/equipment`);
  }

  getMovements(
    siteId: string,
    shiftDate: string,
    shift: ShiftKind,
    status: MovementStatus | ''
  ): Observable<Movement[]> {
    let params = new HttpParams().set('shiftDate', shiftDate).set('shift', shift);
    if (status) {
      params = params.set('status', status);
    }
    return this.http.get<Movement[]>(`/api/sites/${encodeURIComponent(siteId)}/movements`, { params });
  }

  createMovement(siteId: string, request: CreateMovementRequest): Observable<Movement> {
    return this.http.post<Movement>(`/api/sites/${encodeURIComponent(siteId)}/movements`, request);
  }

  updateMovement(siteId: string, movementId: string, request: UpdateMovementRequest): Observable<Movement> {
    return this.http.patch<Movement>(
      `/api/sites/${encodeURIComponent(siteId)}/movements/${encodeURIComponent(movementId)}`,
      request
    );
  }
}

