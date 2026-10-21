import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';

interface RoleRow {
  id: number;
  name: string;
  slug: string;
  isActive: boolean;
  mainEmployees: number;
  users: number;
}

@Component({
  selector: 'app-roles',
  templateUrl: './roles.html',
  styleUrl: './roles.css',
})
export class Roles {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/main/roles`;

  rows = signal<RoleRow[]>([]);
  loading = signal(true);
  busyId = signal<number | null>(null);
  error = signal<string | null>(null);

  modal = signal(false);
  editId = signal<number | null>(null);
  name = signal('');
  saving = signal(false);
  formError = signal<string | null>(null);

  constructor() {
    this.load();
  }

  private msg(e: HttpErrorResponse, fallback: string) {
    return e.status === 0 ? 'Cannot reach the server. Try again.' : (e.error?.message ?? fallback);
  }

  load() {
    this.loading.set(true);
    this.http.get<RoleRow[]>(this.base).subscribe({
      next: (l) => {
        this.rows.set(l);
        this.loading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(this.msg(e, 'Could not load roles.'));
      },
    });
  }

  openNew() {
    this.editId.set(null);
    this.name.set('');
    this.formError.set(null);
    this.modal.set(true);
  }

  openEdit(r: RoleRow) {
    this.editId.set(r.id);
    this.name.set(r.name);
    this.formError.set(null);
    this.modal.set(true);
  }

  closeModal() {
    this.modal.set(false);
  }

  onName(e: Event) {
    this.name.set((e.target as HTMLInputElement).value);
  }

  save() {
    const name = this.name().trim();
    if (name.length < 2) {
      this.formError.set('Role name is required.');
      return;
    }
    this.saving.set(true);
    this.formError.set(null);

    const id = this.editId();
    const call$ =
      id === null
        ? this.http.post(this.base, { name })
        : this.http.put(`${this.base}/${id}`, { name });

    call$.subscribe({
      next: () => {
        this.saving.set(false);
        this.modal.set(false);
        this.load();
      },
      error: (e: HttpErrorResponse) => {
        this.saving.set(false);
        this.formError.set(this.msg(e, 'Could not save role.'));
      },
    });
  }

  toggle(r: RoleRow) {
    this.busyId.set(r.id);
    this.error.set(null);
    this.http
      .patch<{ isActive: boolean }>(`${this.base}/${r.id}/status`, { isActive: !r.isActive })
      .subscribe({
        next: (res) => {
          this.rows.update((l) =>
            l.map((x) => (x.id === r.id ? { ...x, isActive: res.isActive } : x)),
          );
          this.busyId.set(null);
        },
        error: (e: HttpErrorResponse) => {
          this.busyId.set(null);
          this.error.set(this.msg(e, 'Could not change status.'));
        },
      });
  }

  remove(r: RoleRow) {
    if (!confirm(`Delete role "${r.name}"?`)) return;
    this.busyId.set(r.id);
    this.error.set(null);
    this.http.delete(`${this.base}/${r.id}`).subscribe({
      next: () => {
        this.busyId.set(null);
        this.rows.update((l) => l.filter((x) => x.id !== r.id));
      },
      error: (e: HttpErrorResponse) => {
        this.busyId.set(null);
        this.error.set(this.msg(e, 'Could not delete role.'));
      },
    });
  }
}
