import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, finalize, map, of, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { BrandService } from '../brand/brand.service';

export interface CompanyInfo {
  id: number;
  name: string;
  logo: string | null;
  theme: string;
}

export interface AuthUser {
  id: number;
  name: string;
  email: string;
  type: 'owner' | 'employee';
  role: string;
  roleName: string;
  employeeId: number | null;
  employeeCode: string | null;
  profilePicture: string | null;
  company: CompanyInfo;
  permissions: string[];
}

export interface LoginPayload {
  email: string;
  password: string;
  remember: boolean;
  lat?: number;
  lng?: number;
  accuracy?: number;
}

interface LoginResponse {
  expiresAt: string;
  user: AuthUser;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private brand = inject(BrandService);
  private base = `${environment.apiUrl}/company/auth`;

  readonly user = signal<AuthUser | null>(null);
  readonly isLoggedIn = computed(() => this.user() !== null);

  login(payload: LoginPayload): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>(`${this.base}/login`, payload)
      .pipe(tap((res) => this.apply(res.user)));
  }

  me(): Observable<AuthUser> {
    return this.http.get<AuthUser>(`${this.base}/me`).pipe(tap((u) => this.apply(u)));
  }

  // true when a valid cookie session exists (false for missing / edited / expired token)
  restore(): Observable<boolean> {
    return this.me().pipe(
      map(() => true),
      catchError(() => {
        this.user.set(null);
        return of(false);
      }),
    );
  }

  logout(): Observable<unknown> {
    // branding stays in localStorage on purpose, so the login page keeps this company's theme
    return this.http.post(`${this.base}/logout`, {}).pipe(finalize(() => this.user.set(null)));
  }

  private apply(u: AuthUser) {
    this.user.set(u);
    this.brand.set(u.company);
  }
}
