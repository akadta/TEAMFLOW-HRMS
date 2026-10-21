import { Component, computed, inject, input, output, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map } from 'rxjs';
import { PermissionService } from '../../core/rbac/permission.service';
import { Icon, MENU, MenuItem, filterMenu } from '../layout.config';

@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive, Icon],
  templateUrl: './sidebar.html',
  styleUrl: './sidebar.css',
})
export class Sidebar {
  private router = inject(Router);
  private perm = inject(PermissionService);

  collapsed = input(false);
  mobileOpen = input(false);
  closeMobile = output<void>();
  expand = output<void>();

  // menu filtered by the logged-in user's permissions
  menu = computed(() => filterMenu(MENU, (p, superOnly) => this.perm.can(p, superOnly)));
  openId = signal<string | null>(null);

  url = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      map(() => this.router.url),
    ),
    { initialValue: this.router.url },
  );

  asideClass = computed(
    () =>
      'fixed inset-y-0 left-0 z-40 flex w-56 flex-col border-r border-blue-400/15 bg-[#050f33]/85 backdrop-blur-xl transition-all duration-200 lg:sticky lg:top-0 lg:h-screen lg:translate-x-0 ' +
      (this.mobileOpen() ? 'translate-x-0 ' : '-translate-x-full ') +
      (this.collapsed() ? 'lg:w-14' : 'lg:w-56'),
  );

  // hides text only on desktop when collapsed (mobile drawer always shows labels)
  hide = computed(() => (this.collapsed() ? 'lg:hidden' : ''));

  // small icon tile: shown only on desktop when the sidebar is collapsed
  showIcon = computed(() => (this.collapsed() ? 'lg:grid' : ''));

  isGroupActive(item: MenuItem): boolean {
    const u = this.url();
    return !!item.children?.some((c) => u === c.route || u.startsWith(c.route + '/'));
  }

  isOpen(item: MenuItem): boolean {
    return this.openId() === item.id || (this.openId() === null && this.isGroupActive(item));
  }

  toggle(item: MenuItem) {
    if (this.collapsed() && window.innerWidth >= 1024) {
      this.expand.emit();
      this.openId.set(item.id);
      return;
    }
    this.openId.set(this.isOpen(item) ? '' : item.id);
  }
}
