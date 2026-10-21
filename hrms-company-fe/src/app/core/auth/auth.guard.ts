import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from './auth.service';

// protects the layout + dashboard: valid cookie required, otherwise go to login
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.user()) return true;

  // page reload: ask the server to verify the cookie (a tampered token fails here)
  return auth.restore().pipe(map((ok) => (ok ? true : router.createUrlTree(['/login']))));
};
