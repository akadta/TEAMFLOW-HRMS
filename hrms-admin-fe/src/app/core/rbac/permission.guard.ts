import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { PermissionService } from './permission.service';

// route data: { permission: 'x' | ['x','y'], superOnly: true }
export const permissionGuard: CanActivateFn = (route) => {
  const perm = inject(PermissionService);
  const router = inject(Router);
  const ok = perm.can(route.data['permission'], !!route.data['superOnly']);
  return ok || router.parseUrl('/dashboard');
};
