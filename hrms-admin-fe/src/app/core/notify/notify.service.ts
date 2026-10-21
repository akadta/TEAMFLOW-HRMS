import { HttpClient } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';

export interface Notice {
  id: number;
  kind: string;
  title: string;
  text: string;
  link: string | null;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class NotifyService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private auth = inject(AuthService);
  private api = `${environment.apiUrl}/main/notifications`;
  private hub = environment.apiUrl.replace(/\/api\/?$/, '') + '/hubs/main';
  private conn: HubConnection | null = null;

  readonly queue = signal<Notice[]>([]);
  readonly show = signal(false);
  readonly pulse = signal(0); // bumps on every new alert, dashboards refresh on it
  readonly list = computed(() => [...this.queue()].sort((a, b) => b.id - a.id)); // newest first

  constructor() {
    effect(() => {
      const loggedIn = !!this.auth.user();
      untracked(() => (loggedIn ? void this.start() : void this.stop()));
    });
  }

  private async start() {
    if (this.conn) return;
    this.fetchUnseen();

    const c = new HubConnectionBuilder()
      .withUrl(this.hub, { withCredentials: true })
      .withAutomaticReconnect()
      .build();
    c.on('notify', (n: Notice) => this.add([n]));
    c.onreconnected(() => this.fetchUnseen());
    this.conn = c;

    try {
      await c.start();
    } catch {
      this.conn = null;
      setTimeout(() => {
        if (this.auth.user()) void this.start();
      }, 10000);
    }
  }

  private async stop() {
    const c = this.conn;
    this.conn = null;
    this.queue.set([]);
    this.show.set(false);
    if (c && c.state !== HubConnectionState.Disconnected) await c.stop();
  }

  private fetchUnseen() {
    this.http
      .get<Notice[]>(`${this.api}/unseen`)
      .subscribe({ next: (l) => this.add(l), error: () => {} });
  }

  private add(list: Notice[]) {
    const have = new Set(this.queue().map((n) => n.id));
    const fresh = list.filter((n) => !have.has(n.id));
    if (fresh.length) {
      this.queue.update((q) => [...q, ...fresh]);
      this.pulse.update((p) => p + 1);
      this.show.set(true);
    }
  }

  hide() {
    this.show.set(false);
  }

  reopen() {
    this.show.set(true);
  }

  dismiss(n: Notice, open = false) {
    this.queue.update((q) => q.filter((x) => x.id !== n.id));
    this.http.post(`${this.api}/${n.id}/seen`, {}).subscribe({ error: () => {} });
    if (this.queue().length === 0) this.show.set(false);
    if (open && n.link) {
      this.show.set(false);
      void this.router.navigateByUrl(n.link);
    }
  }

  dismissAll() {
    if (this.queue().length === 0) return;
    this.queue.set([]);
    this.show.set(false);
    this.http.post(`${this.api}/seen-all`, {}).subscribe({ error: () => {} });
  }
}
