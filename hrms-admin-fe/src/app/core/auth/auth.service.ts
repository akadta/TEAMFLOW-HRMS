import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { catchError, map, of, tap } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface LoginBody {
  email: string;
  password: string;
  remember: boolean;
}

export interface UserProfile {
  id: number;
  name: string;
  email: string;
  role: string;
  isSuperAdmin: boolean;
  permissions: string[];
}

export interface AuthResponse {
  expiresAt: string;
  user: UserProfile;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/main/auth`;

  user = signal<UserProfile | null>(null);

  login(body: LoginBody) {
    return this.http
      .post<AuthResponse>(`${this.base}/login`, body)
      .pipe(tap((res) => this.user.set(res.user)));
  }

  checkSession() {
    return this.http.get<UserProfile>(`${this.base}/me`).pipe(
      tap((u) => this.user.set(u)),
      map(() => true),
      catchError(() => {
        this.user.set(null);
        return of(false);
      }),
    );
  }

  logout() {
    return this.http.post(`${this.base}/logout`, {}).pipe(tap(() => this.user.set(null)));
  }
}
