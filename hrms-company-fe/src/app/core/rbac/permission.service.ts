import { Injectable, computed, inject } from '@angular/core';
import { AuthService } from '../auth/auth.service';

@Injectable({ providedIn: 'root' })
export class PermissionService {
  private auth = inject(AuthService);

  readonly isOwner = computed(() => this.auth.user()?.type === 'owner');
  private readonly granted = computed(() => new Set(this.auth.user()?.permissions ?? []));

  // can('employees.list')            -> that slug
  // can(['a', 'b'])                  -> any of them
  // can(undefined)                   -> true (no permission needed)
  // can(undefined, true)             -> owner only
  can(permission?: string | string[], ownerOnly = false): boolean {
    if (!this.auth.user()) return false;
    if (this.isOwner()) return true;
    if (ownerOnly) return false;
    if (!permission || permission.length === 0) return true;
    const need = Array.isArray(permission) ? permission : [permission];
    const set = this.granted();
    return need.some((s) => set.has(s));
  }
}
