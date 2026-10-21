import {
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { filter, map } from 'rxjs';
import { BrandService } from '../../core/brand/brand.service';
import { PermissionService } from '../../core/rbac/permission.service';
import { LAYOUT_CONFIG, SidebarItem } from '../layout.config';
import { BrandLogo } from '../../core/brand/brand-logo';
@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive, BrandLogo],
  templateUrl: './sidebar.html',
  styleUrl: './sidebar.css',
})
export class Sidebar {
  readonly brand = inject(BrandService);
  private perm = inject(PermissionService);
  private router = inject(Router);

  readonly cfg = LAYOUT_CONFIG.sidebar;
  readonly collapsed = input(false);
  readonly expand = output<void>();

  private open = signal<ReadonlySet<string>>(new Set());
  private url = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      map(() => this.router.url),
    ),
    { initialValue: this.router.url },
  );

  // menu filtered by permission; groups with no visible child disappear
  readonly items = computed<SidebarItem[]>(() =>
    this.cfg.items
      .map((i) =>
        i.children
          ? { ...i, children: i.children.filter((c) => this.perm.can(c.permission, c.ownerOnly)) }
          : i,
      )
      .filter((i) =>
        i.children ? i.children.length > 0 : this.perm.can(i.permission, i.ownerOnly),
      ),
  );

  constructor() {
    // open the group that contains the current page
    effect(() => {
      const path = this.url().split('?')[0];
      const groups = this.cfg.items
        .filter((g) =>
          g.children?.some((c) => c.route && (path === c.route || path.startsWith(c.route + '/'))),
        )
        .map((g) => g.label);
      if (groups.length) untracked(() => this.open.update((s) => new Set([...s, ...groups])));
    });
  }

  isOpen(item: SidebarItem) {
    return this.open().has(item.label);
  }

  toggleGroup(item: SidebarItem) {
    if (this.collapsed()) {
      this.expand.emit();
      this.open.update((s) => new Set([...s, item.label]));
      return;
    }
    this.open.update((s) => {
      const n = new Set(s);
      n.has(item.label) ? n.delete(item.label) : n.add(item.label);
      return n;
    });
  }
}
