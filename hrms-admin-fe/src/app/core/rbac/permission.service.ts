import { Injectable, computed, inject } from '@angular/core';
import { AuthService } from '../auth/auth.service';

@Injectable({ providedIn: 'root' })
export class PermissionService {
  private auth = inject(AuthService);

  readonly isSuper = computed(() => !!this.auth.user()?.isSuperAdmin);
  private readonly granted = computed(() => new Set(this.auth.user()?.permissions ?? []));

  // can('employees.list')   -> that slug
  // can(['a', 'b'])         -> any of them
  // can(undefined)          -> true (no permission needed)
  // can(undefined, true)    -> super admin only
  can(permission?: string | string[], superOnly = false): boolean {
    if (!this.auth.user()) return false;
    if (this.isSuper()) return true;
    if (superOnly) return false;
    if (!permission || permission.length === 0) return true;
    const need = Array.isArray(permission) ? permission : [permission];
    const set = this.granted();
    return need.some((s) => set.has(s));
  }
}
