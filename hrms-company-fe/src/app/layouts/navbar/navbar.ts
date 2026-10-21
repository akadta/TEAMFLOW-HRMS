import {
  Component,
  ElementRef,
  HostListener,
  computed,
  effect,
  inject,
  output,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/auth/auth.service';
import { BrandLogo } from '../../core/brand/brand-logo';
import { BrandService } from '../../core/brand/brand.service';
import { LAYOUT_CONFIG, ProfileMenuItem } from '../layout.config';

@Component({
  selector: 'app-navbar',
  imports: [BrandLogo],
  templateUrl: './navbar.html',
  styleUrl: './navbar.css',
})
export class Navbar {
  readonly brand = inject(BrandService);
  readonly auth = inject(AuthService);
  private router = inject(Router);
  private host = inject<ElementRef<HTMLElement>>(ElementRef);
  private root = environment.apiUrl.replace(/\/api\/?$/, '');

  readonly cfg = LAYOUT_CONFIG.navbar;
  readonly toggle = output<void>();
  readonly open = signal(false);

  readonly initial = computed(() => (this.auth.user()?.name.trim()[0] ?? 'U').toUpperCase());

  // set when the picture file fails to load, so the initial avatar shows instead
  readonly photoFailed = signal(false);

  // employee profile picture url, or null (owners and employees without a picture)
  readonly photo = computed(() => {
    const p = this.auth.user()?.profilePicture;
    if (!p || this.photoFailed()) return null;
    return /^https?:/i.test(p) ? p : this.root + p;
  });

  constructor() {
    // a different user signed in: try the picture again
    effect(() => {
      this.auth.user();
      this.photoFailed.set(false);
    });
  }

  // close the dropdown on outside click / Esc
  @HostListener('document:click', ['$event'])
  onDocClick(e: MouseEvent) {
    if (!this.host.nativeElement.contains(e.target as Node)) this.open.set(false);
  }

  @HostListener('document:keydown.escape')
  onEsc() {
    this.open.set(false);
  }

  onMenu(item: ProfileMenuItem) {
    this.open.set(false);
    if (item.action === 'logout') {
      this.logout();
      return;
    }
    if (item.route) this.router.navigateByUrl(item.route);
  }

  private logout() {
    const goLogin = () => this.router.navigateByUrl('/login');
    this.auth.logout().subscribe({ next: goLogin, error: goLogin });
  }
}
