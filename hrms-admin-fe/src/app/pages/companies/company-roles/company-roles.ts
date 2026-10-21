import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { environment } from '../../../../environments/environment';

interface CompanyOption {
  id: number;
  name: string;
  isActive: boolean;
}

interface CompanyRole {
  id: number;
  companyId: number;
  name: string;
  slug: string;
  isActive: boolean;
}

@Component({
  selector: 'app-company-roles',
  imports: [],
  templateUrl: './company-roles.html',
  styleUrl: './company-roles.css',
})
export class CompanyRoles {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/main/company-roles`;

  companies = signal<CompanyOption[]>([]);
  companyId = signal<number | null>(null);
  roles = signal<CompanyRole[]>([]);

  loadingCompanies = signal(true);
  loadingRoles = signal(false);
  adding = signal(false);
  busyId = signal<number | null>(null);
  editingId = signal<number | null>(null);
  editValue = signal('');
  search = signal('');
  error = signal<string | null>(null);

  constructor() {
    this.http.get<CompanyOption[]>(`${this.base}/companies`).subscribe({
      next: (list) => {
        this.companies.set(list);
        this.loadingCompanies.set(false);
      },
      error: () => {
        this.loadingCompanies.set(false);
        this.error.set('Could not load companies.');
      },
    });
  }

  private msg(e: HttpErrorResponse, fallback: string) {
    return e.status === 0 ? 'Cannot reach the server. Try again.' : (e.error?.message ?? fallback);
  }

  private sorted(list: CompanyRole[]) {
    return [...list].sort((a, b) => a.name.localeCompare(b.name));
  }

  startEdit(r: CompanyRole) {
    this.editValue.set(r.name);
    this.editingId.set(r.id);
  }

  filtered() {
    const q = this.search().trim().toLowerCase();
    if (!q) return this.roles();
    return this.roles().filter(
      (r) => r.name.toLowerCase().includes(q) || r.slug.toLowerCase().includes(q),
    );
  }

  selectedName() {
    return this.companies().find((c) => c.id === this.companyId())?.name ?? '';
  }

  onPick(e: Event) {
    const v = (e.target as HTMLSelectElement).value;
    this.editingId.set(null);
    this.search.set('');
    this.error.set(null);

    if (!v) {
      this.companyId.set(null);
      this.roles.set([]);
      return;
    }

    const id = Number(v);
    this.companyId.set(id);
    this.loadingRoles.set(true);
    this.roles.set([]);

    this.http.get<CompanyRole[]>(this.base, { params: { companyId: id } }).subscribe({
      next: (list) => {
        this.roles.set(list);
        this.loadingRoles.set(false);
      },
      error: () => {
        this.loadingRoles.set(false);
        this.error.set('Could not load roles.');
      },
    });
  }

  add(input: HTMLInputElement) {
    const name = input.value.trim();
    const companyId = this.companyId();
    if (!name || companyId === null || this.adding()) return;

    this.adding.set(true);
    this.error.set(null);

    this.http.post<CompanyRole>(this.base, { companyId, name }).subscribe({
      next: (r) => {
        this.roles.update((l) => this.sorted([...l, r]));
        input.value = '';
        this.adding.set(false);
        input.focus();
      },
      error: (e: HttpErrorResponse) => {
        this.adding.set(false);
        this.error.set(this.msg(e, 'Could not add role.'));
      },
    });
  }

  saveEdit(r: CompanyRole, value: string) {
    const name = value.trim();
    if (!name) return;
    if (name === r.name) {
      this.editingId.set(null);
      return;
    }

    this.busyId.set(r.id);
    this.error.set(null);

    this.http.put<CompanyRole>(`${this.base}/${r.id}`, { name }).subscribe({
      next: (u) => {
        this.roles.update((l) => this.sorted(l.map((x) => (x.id === r.id ? u : x))));
        this.busyId.set(null);
        this.editingId.set(null);
      },
      error: (e: HttpErrorResponse) => {
        this.busyId.set(null);
        this.error.set(this.msg(e, 'Could not update role.'));
      },
    });
  }

  toggle(r: CompanyRole) {
    this.busyId.set(r.id);
    this.error.set(null);

    this.http
      .patch<{ isActive: boolean }>(`${this.base}/${r.id}/status`, { isActive: !r.isActive })
      .subscribe({
        next: (res) => {
          this.roles.update((l) =>
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

  remove(r: CompanyRole) {
    if (!confirm(`Delete role "${r.name}"?`)) return;

    this.busyId.set(r.id);
    this.error.set(null);

    this.http.delete(`${this.base}/${r.id}`).subscribe({
      next: () => {
        this.roles.update((l) => l.filter((x) => x.id !== r.id));
        this.busyId.set(null);
      },
      error: (e: HttpErrorResponse) => {
        this.busyId.set(null);
        this.error.set(this.msg(e, 'Could not delete role.'));
      },
    });
  }
}
