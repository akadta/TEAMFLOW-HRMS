import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { errMsg } from '../../attendance/attendance.util';
import { BrandService } from '../../core/brand/brand.service';

interface Role {
  id: number;
  name: string;
  slug: string;
  isActive: boolean;
  employees: number;
}

interface Dialog {
  title: string;
  text: string;
  confirm?: () => void; // present = confirm dialog (Cancel + Yes), absent = info/error (OK)
  danger?: boolean;
}

@Component({
  selector: 'app-company-roles',
  templateUrl: './company-roles.html',
  styleUrl: '../../attendance/attendance.css',
})
export class CompanyRoles {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/company/roles`;
  readonly brand = inject(BrandService);

  roles = signal<Role[]>([]);
  loading = signal(true);
  busy = signal(false);
  search = signal('');
  form = signal<{ id: number | null; name: string } | null>(null);
  dialog = signal<Dialog | null>(null);

  shown = computed(() => {
    const q = this.search().trim().toLowerCase();
    return q ? this.roles().filter((r) => r.name.toLowerCase().includes(q)) : this.roles();
  });

  constructor() {
    this.load();
  }

  val(e: Event) {
    return (e.target as HTMLInputElement).value;
  }

  // ---------- dialogs ----------
  private say(title: string, text: string) {
    this.dialog.set({ title, text });
  }

  private ask(title: string, text: string, confirm: () => void, danger = false) {
    this.dialog.set({ title, text, confirm, danger });
  }

  yes() {
    const d = this.dialog();
    this.dialog.set(null);
    d?.confirm?.();
  }

  // ---------- data ----------
  load() {
    this.http.get<Role[]>(this.api).subscribe({
      next: (l) => {
        this.roles.set(l);
        this.loading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.say('Could not load roles', errMsg(e, 'Could not load roles.'));
      },
    });
  }

  add() {
    this.form.set({ id: null, name: '' });
  }

  edit(r: Role) {
    this.form.set({ id: r.id, name: r.name });
  }

  setName(v: string) {
    this.form.update((f) => (f ? { ...f, name: v } : f));
  }

  save() {
    const f = this.form();
    if (!f || this.busy()) return;

    const name = f.name.trim();
    if (!name) return this.say('Role name', 'Enter a role name.');

    this.busy.set(true);
    const req = f.id
      ? this.http.put(`${this.api}/${f.id}`, { name })
      : this.http.post(this.api, { name });

    req.subscribe({
      next: () => {
        this.busy.set(false);
        this.form.set(null);
        this.load();
      },
      error: (e: HttpErrorResponse) => {
        this.busy.set(false);
        this.say('Could not save', errMsg(e, 'Could not save role.'));
      },
    });
  }

  toggle(r: Role) {
    const next = !r.isActive;
    const go = () =>
      this.http.patch(`${this.api}/${r.id}/status`, { isActive: next }).subscribe({
        next: () =>
          this.roles.update((l) => l.map((x) => (x.id === r.id ? { ...x, isActive: next } : x))),
        error: (e: HttpErrorResponse) =>
          this.say('Could not update', errMsg(e, 'Could not update status.')),
      });

    if (!next && r.employees > 0)
      this.ask(
        'Deactivate role',
        `${r.employees} employee(s) have the role "${r.name}". They will not be able to sign in until it is active again.`,
        go,
        true,
      );
    else go();
  }

  remove(r: Role) {
    this.ask(
      'Delete role',
      `Delete "${r.name}"? This cannot be undone.`,
      () =>
        this.http.delete(`${this.api}/${r.id}`).subscribe({
          next: () => this.roles.update((l) => l.filter((x) => x.id !== r.id)),
          error: (e: HttpErrorResponse) =>
            this.say('Could not delete', errMsg(e, 'Could not delete role.')),
        }),
      true,
    );
  }
}
