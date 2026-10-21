import { Component, HostListener, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { BrandService } from '../core/brand/brand.service';
import { Navbar } from './navbar/navbar';
import { Sidebar } from './sidebar/sidebar';
import { NotifyModal } from '../core/notify/notify-modal';
@Component({
  selector: 'app-layouts',
  imports: [RouterOutlet, Sidebar, Navbar, NotifyModal],
  templateUrl: './layouts.html',
  styleUrl: './layouts.css',
})
export class Layouts {
  readonly brand = inject(BrandService);
  private router = inject(Router);

  readonly mobile = signal(window.innerWidth < 768);
  readonly collapsed = signal(window.innerWidth < 768);

  constructor() {
    // close the menu after choosing a page on mobile
    this.router.events
      .pipe(
        filter((e) => e instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => {
        if (this.mobile()) this.collapsed.set(true);
      });
  }

  @HostListener('window:resize')
  onResize() {
    const m = window.innerWidth < 768;
    if (m !== this.mobile()) {
      this.mobile.set(m);
      this.collapsed.set(m);
    }
  }

  toggleSidebar() {
    this.collapsed.update((v) => !v);
  }
}
