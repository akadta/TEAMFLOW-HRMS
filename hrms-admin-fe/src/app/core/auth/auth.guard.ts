import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from './auth.service';

// protected pages: verifies the cookie with the server on every navigation
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.checkSession().pipe(map((ok) => (ok ? true : router.createUrlTree(['/']))));
};

// login page: already logged in -> dashboard
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.checkSession().pipe(map((ok) => (ok ? router.createUrlTree(['/dashboard']) : true)));
};
