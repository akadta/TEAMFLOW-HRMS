import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { environment } from '../../../../environments/environment';

interface RoleOpt {
  id: number;
  name: string;
  isActive: boolean;
}
interface Emp {
  id: number;
  employeeCode: string;
  name: string;
  email: string;
  roleId: number;
  role: string;
}
interface Perm {
  id: number;
  name: string;
  slug: string;
}
interface Heading {
  id: number;
  name: string;
  slug: string;
  permissions: Perm[];
}

@Component({
  selector: 'app-main-employee-permission',
  imports: [],
  templateUrl: './main-employee-permission.html',
  styleUrl: './main-employee-permission.css',
})
export class MainEmployeePermission {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/main/main-permissions`;
  private timer: ReturnType<typeof setTimeout> | undefined;

  roles = signal<RoleOpt[]>([]);
  employees = signal<Emp[]>([]);
  catalog = signal<Heading[]>([]);

  roleId = signal('');
  search = signal('');
  selected = signal<Emp | null>(null);
  granted = signal<ReadonlySet<number>>(new Set());
  saved = signal<ReadonlySet<number>>(new Set());

  listLoading = signal(false);
  loading = signal(false);
  saving = signal(false);
  error = signal<string | null>(null);
  success = signal<string | null>(null);

  total = computed(() => this.catalog().reduce((n, h) => n + h.permissions.length, 0));

  dirty = computed(() => {
    const a = this.granted();
    const b = this.saved();
    return a.size !== b.size || [...a].some((id) => !b.has(id));
  });

  constructor() {
    this.http.get<RoleOpt[]>(`${this.api}/roles`).subscribe({
      next: (l) => this.roles.set(l),
      error: (e: HttpErrorResponse) => this.error.set(this.msg(e, 'Could not load roles.')),
    });
    this.http.get<Heading[]>(`${this.api}/catalog`).subscribe({
      next: (l) => this.catalog.set(l),
      error: (e: HttpErrorResponse) => this.error.set(this.msg(e, 'Could not load permissions.')),
    });
    this.loadEmployees();
  }

  private msg(e: HttpErrorResponse, fallback: string) {
    return e.status === 0 ? 'Cannot reach the server. Try again.' : (e.error?.message ?? fallback);
  }

  initial(e: Emp) {
    return (e.name.trim()[0] ?? '?').toUpperCase();
  }

  // ---------- employee list + role filter ----------
  loadEmployees() {
    this.listLoading.set(true);
    let params = new HttpParams();
    if (this.roleId()) params = params.set('roleId', this.roleId());
    if (this.search().trim()) params = params.set('search', this.search().trim());

    this.http.get<Emp[]>(`${this.api}/employees`, { params }).subscribe({
      next: (l) => {
        this.employees.set(l);
        this.listLoading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.listLoading.set(false);
        this.error.set(this.msg(e, 'Could not load employees.'));
      },
    });
  }

  onRole(e: Event) {
    this.roleId.set((e.target as HTMLSelectElement).value);
    this.loadEmployees();
  }

  onSearch(e: Event) {
    this.search.set((e.target as HTMLInputElement).value);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.loadEmployees(), 350);
  }

  // ---------- selection ----------
  select(emp: Emp) {
    if (this.selected()?.id === emp.id) return;
    if (this.dirty() && !confirm('Discard unsaved changes?')) return;

    this.selected.set(emp);
    this.error.set(null);
    this.success.set(null);
    this.loading.set(true);
    this.http.get<{ permissionIds: number[] }>(`${this.api}/employee/${emp.id}`).subscribe({
      next: (r) => {
        this.granted.set(new Set(r.permissionIds));
        this.saved.set(new Set(r.permissionIds));
        this.loading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(this.msg(e, 'Could not load employee permissions.'));
      },
    });
  }

  // ---------- ticking ----------
  has(id: number) {
    return this.granted().has(id);
  }

  toggle(id: number) {
    this.granted.update((s) => {
      const n = new Set(s);
      n.has(id) ? n.delete(id) : n.add(id);
      return n;
    });
  }

  countIn(h: Heading) {
    const g = this.granted();
    return h.permissions.filter((p) => g.has(p.id)).length;
  }

  state(h: Heading): 'all' | 'some' | 'none' {
    const c = this.countIn(h);
    return c === 0 ? 'none' : c === h.permissions.length ? 'all' : 'some';
  }

  toggleHeading(h: Heading) {
    const all = this.state(h) === 'all';
    this.granted.update((s) => {
      const n = new Set(s);
      h.permissions.forEach((p) => (all ? n.delete(p.id) : n.add(p.id)));
      return n;
    });
  }

  selectAll() {
    this.granted.set(new Set(this.catalog().flatMap((h) => h.permissions.map((p) => p.id))));
  }

  clearAll() {
    this.granted.set(new Set());
  }

  // ---------- save ----------
  save() {
    const emp = this.selected();
    if (!emp || this.saving()) return;
    this.saving.set(true);
    this.error.set(null);
    this.success.set(null);

    this.http
      .put<{ count: number }>(`${this.api}/employee/${emp.id}`, {
        permissionIds: [...this.granted()],
      })
      .subscribe({
        next: (r) => {
          this.saving.set(false);
          this.saved.set(new Set(this.granted()));
          this.success.set(
            `Saved. ${emp.name} now has ${r.count} permission${r.count === 1 ? '' : 's'}.`,
          );
        },
        error: (e: HttpErrorResponse) => {
          this.saving.set(false);
          this.error.set(this.msg(e, 'Could not save permissions.'));
        },
      });
  }
}
