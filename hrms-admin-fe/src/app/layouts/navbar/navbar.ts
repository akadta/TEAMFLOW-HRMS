import { Component, ElementRef, HostListener, inject, output, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { Icon, USER_MENU } from '../layout.config';

@Component({
  selector: 'app-navbar',
  imports: [RouterLink, Icon],
  templateUrl: './navbar.html',
  styleUrl: './navbar.css',
})
export class Navbar {
  private router = inject(Router);
  private host = inject(ElementRef<HTMLElement>);
  private auth = inject(AuthService);

  toggleSidebar = output<void>();

  userMenu = USER_MENU;
  open = signal(false);

  get user() {
    const u = this.auth.user();
    const name = u?.name ?? '';
    return {
      name,
      role: (u?.role ?? '')
        .split('-')
        .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
        .join(' '),
      initials: name
        .split(' ')
        .filter(Boolean)
        .slice(0, 2)
        .map((w) => w[0].toUpperCase())
        .join(''),
    };
  }

  @HostListener('document:click', ['$event'])
  onDocClick(e: Event) {
    if (!this.host.nativeElement.contains(e.target)) this.open.set(false);
  }

  logout() {
    this.open.set(false);
    this.auth.logout().subscribe({
      next: () => this.router.navigateByUrl('/'),
      error: () => this.router.navigateByUrl('/'),
    });
  }
}
