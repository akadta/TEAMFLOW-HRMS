import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Sidebar } from './sidebar/sidebar';
import { Navbar } from './navbar/navbar';
import { NotifyModal } from '../core/notify/notify-modal';
@Component({
  selector: 'app-layouts',
  imports: [RouterOutlet, Sidebar, Navbar, NotifyModal],
  templateUrl: './layouts.html',
  styleUrl: './layouts.css',
})
export class Layouts {
  collapsed = signal(false); // desktop: icon-only rail
  mobileOpen = signal(false); // mobile: drawer

  toggleSidebar() {
    if (window.innerWidth >= 1024) this.collapsed.update((v) => !v);
    else this.mobileOpen.update((v) => !v);
  }
}
