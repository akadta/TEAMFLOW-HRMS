import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { errMsg } from '../../attendance/attendance.util';
import { BrandService } from '../../core/brand/brand.service';

type Scope = 'company' | 'employee';

interface Cfg {
  managers: boolean;
  owner: boolean;
  main: boolean;
  mode: string;
}
interface RoleOption {
  id: number;
  name: string;
  isActive: boolean;
}
interface EmpOption {
  userId: number;
  code: string;
  name: string;
  role: string | null;
  hasOverride: boolean;
  overrideActive: boolean;
}

@Component({
  selector: 'app-task-settings',
  templateUrl: './task-settings.html',
  styleUrl: '../../attendance/attendance.css',
})
export class TaskSettings {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/company/tasks/settings`;
  private timer?: ReturnType<typeof setTimeout>;
  readonly brand = inject(BrandService);

  scope = signal<Scope>('company');
  roles = signal<RoleOption[]>([]);
  roleId = signal('');
  search = signal('');
  emps = signal<EmpOption[]>([]);
  userId = signal<number | null>(null);
  hasOwn = signal(false);

  isActive = signal(false);
  required = signal(false);
  cfg = signal<Cfg>({ managers: true, owner: false, main: false, mode: 'any' });
  saving = signal(false);
  error = signal<string | null>(null);
  success = signal<string | null>(null);

  ready = computed(() => this.scope() === 'company' || this.userId() !== null);

  constructor() {
    this.load();
  }

  private q(): Record<string, number> {
    return this.scope() === 'employee' ? { userId: this.userId()! } : {};
  }

  val(e: Event) {
    return (e.target as HTMLInputElement).value;
  }

  set<K extends keyof Cfg>(k: K, v: Cfg[K]) {
    this.cfg.update((c) => ({ ...c, [k]: v }));
  }

  private reset() {
    this.isActive.set(false);
    this.required.set(false);
    this.cfg.set({ managers: true, owner: false, main: false, mode: 'any' });
    this.hasOwn.set(false);
    this.error.set(null);
    this.success.set(null);
  }

  pickScope(s: Scope) {
    this.scope.set(s);
    this.userId.set(null);
    this.reset();
    if (s === 'employee') this.loadPeople();
    this.load();
  }

  private loadPeople() {
    this.http
      .get<RoleOption[]>(`${environment.apiUrl}/company/roles`)
      .subscribe({ next: (l) => this.roles.set(l.filter((r) => r.isActive)) });
    this.loadEmps();
  }

  private loadEmps() {
    const params: Record<string, string | number> = {};
    if (this.roleId()) params['roleId'] = Number(this.roleId());
    if (this.search().trim()) params['search'] = this.search().trim();
    this.http
      .get<EmpOption[]>(`${this.api}/employees`, { params })
      .subscribe({ next: (l) => this.emps.set(l) });
  }

  pickRole(e: Event) {
    this.roleId.set((e.target as HTMLSelectElement).value);
    this.loadEmps();
  }

  onSearch(e: Event) {
    this.search.set(this.val(e));
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.loadEmps(), 300);
  }

  pickEmp(id: number) {
    this.userId.set(id);
    this.reset();
    this.load();
  }

  private load() {
    if (!this.ready()) return;
    this.http
      .get<{ isActive: boolean; approvalRequired: boolean; config: Cfg; hasOwn: boolean }>(
        this.api,
        { params: this.q() },
      )
      .subscribe({
        next: (r) => {
          this.isActive.set(r.isActive);
          this.required.set(r.approvalRequired);
          this.hasOwn.set(r.hasOwn);
          this.cfg.set({
            managers: !!r.config.managers,
            owner: !!r.config.owner,
            main: !!r.config.main,
            mode: r.config.mode === 'all' ? 'all' : 'any',
          });
        },
        error: (e: HttpErrorResponse) => this.error.set(errMsg(e, 'Could not load settings.')),
      });
  }

  save() {
    if (this.saving() || !this.ready()) return;
    this.saving.set(true);
    this.error.set(null);
    this.success.set(null);
    this.http
      .put(
        this.api,
        { isActive: this.isActive(), approvalRequired: this.required(), config: this.cfg() },
        { params: this.q() },
      )
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.success.set('Saved.');
          this.hasOwn.set(true);
          if (this.scope() === 'employee') this.loadEmps();
        },
        error: (e: HttpErrorResponse) => {
          this.saving.set(false);
          this.error.set(errMsg(e, 'Could not save.'));
        },
      });
  }

  removeOwn() {
    if (
      !confirm("Remove this employee's own settings? They will follow the company setting again.")
    )
      return;
    this.http.delete(this.api, { params: this.q() }).subscribe({
      next: () => {
        this.reset();
        this.load();
        this.loadEmps();
      },
      error: (e: HttpErrorResponse) => this.error.set(errMsg(e, 'Could not remove.')),
    });
  }
}
