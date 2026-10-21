import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(environment.apiUrl)) {
    return next(req);
  }

  const auth = inject(AuthService);
  const router = inject(Router);

  // send the HttpOnly cookie with every API call
  const request = req.clone({ withCredentials: true });

  return next(request).pipe(
    catchError((err: HttpErrorResponse) => {
      const isAuthCall = req.url.includes('/auth/login') || req.url.includes('/auth/me');
      if (err.status === 401 && !isAuthCall) {
        auth.user.set(null);
        router.navigateByUrl('/login');
      }
      return throwError(() => err);
    }),
  );
};
