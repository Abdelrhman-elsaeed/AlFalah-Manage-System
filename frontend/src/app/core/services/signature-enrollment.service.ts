import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { AuthService } from './auth.service';

export interface SignatureRequirement { required: boolean; hasSignature: boolean; }
export interface SignatureData { signatureDrawnData: string | null; }

@Injectable({ providedIn: 'root' })
export class SignatureEnrollmentService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly base = `${environment.apiUrl}/api/v1/account/signature`;
  private cached: { key: string; response: ApiResponse<SignatureRequirement> } | null = null;
  requirement(): Observable<ApiResponse<SignatureRequirement>> {
    const key = `${this.auth.currentUser()?.userId}:${this.auth.getAccessToken()}`;
    if (this.cached?.key === key) return of(this.cached.response);
    return this.http.get<ApiResponse<SignatureRequirement>>(`${this.base}/requirement`).pipe(tap(response => {
      if (response.isSuccess && response.data) this.cached = { key, response };
    }));
  }
  load(): Observable<ApiResponse<SignatureData>> { return this.http.get<ApiResponse<SignatureData>>(this.base); }
  save(signatureDrawnData: string): Observable<ApiResponse<boolean>> {
    return this.http.put<ApiResponse<boolean>>(this.base, { signatureDrawnData }).pipe(tap(response => {
      if (response.isSuccess) this.cached = null;
    }));
  }
  invalidate(): void { this.cached = null; }
}
