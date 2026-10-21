import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { PermissionService } from './permission.service';

// route data: { permission: 'employees.list' | ['a','b'], ownerOnly: true }
export const permissionGuard: CanActivateFn = (route) => {
  const perm = inject(PermissionService);
  const router = inject(Router);
  const ok = perm.can(route.data['permission'], !!route.data['ownerOnly']);
  return ok || router.parseUrl('/dashboard');
};
