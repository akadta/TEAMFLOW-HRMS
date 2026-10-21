import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { errMsg } from '../../my-companies/mc.util';

type Scope = 'global' | 'company';

interface Cfg {
  managers: boolean;
  owner: boolean;
  main: boolean;
  mode: string;
}
interface CompanyOption {
  id: number;
  name: string;
  isActive: boolean;
}

@Component({
  selector: 'app-leave-settings',
  templateUrl: './leave-settings.html',
})
export class LeaveSettings {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/main/leave-settings`;

  readonly scopes: { id: Scope; label: string }[] = [
    { id: 'global', label: 'Global' },
    { id: 'company', label: 'Company-wise' },
  ];
  readonly inp =
    'rounded-lg border border-slate-200 bg-white/80 px-2.5 py-1.5 text-xs text-slate-900 outline-none transition focus:border-blue-400 focus:ring-2 focus:ring-blue-200';

  scope = signal<Scope>('global');
  companies = signal<CompanyOption[]>([]);
  companyId = signal<number | null>(null);
  isActive = signal(false);
  maxDays = signal('');
  cfg = signal<Cfg>({ managers: true, owner: false, main: false, mode: 'any' });
  saving = signal(false);
  error = signal<string | null>(null);
  success = signal<string | null>(null);

  ready = computed(() => this.scope() === 'global' || this.companyId() !== null);

  constructor() {
    this.http.get<CompanyOption[]>(`${environment.apiUrl}/main/company-roles/companies`).subscribe({
      next: (l) => this.companies.set(l),
      error: () => this.error.set('Could not load companies.'),
    });
    this.load();
  }

  private q(): Record<string, number> {
    return this.scope() === 'company' ? { companyId: this.companyId()! } : {};
  }

  val(e: Event) {
    return (e.target as HTMLInputElement).value;
  }

  set<K extends keyof Cfg>(k: K, v: Cfg[K]) {
    this.cfg.update((c) => ({ ...c, [k]: v }));
  }

  pickScope(s: Scope) {
    this.scope.set(s);
    this.reset();
    this.load();
  }

  pickCompany(e: Event) {
    const v = (e.target as HTMLSelectElement).value;
    this.companyId.set(v ? Number(v) : null);
    this.reset();
    this.load();
  }

  private reset() {
    this.isActive.set(false);
    this.maxDays.set('');
    this.cfg.set({ managers: true, owner: false, main: false, mode: 'any' });
    this.error.set(null);
    this.success.set(null);
  }

  private load() {
    if (!this.ready()) return;
    this.http
      .get<{ isActive: boolean; maxDays: number | null; config: Cfg }>(this.api, {
        params: this.q(),
      })
      .subscribe({
        next: (r) => {
          this.isActive.set(r.isActive);
          this.maxDays.set(r.maxDays == null ? '' : String(r.maxDays));
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

    const md = this.maxDays().trim();
    if (md !== '' && (!/^\d+$/.test(md) || Number(md) < 1 || Number(md) > 365)) {
      this.success.set(null);
      return this.error.set('Max days must be between 1 and 365, or empty for no limit.');
    }

    this.saving.set(true);
    this.error.set(null);
    this.success.set(null);
    this.http
      .put(
        this.api,
        { isActive: this.isActive(), maxDays: md === '' ? null : Number(md), config: this.cfg() },
        { params: this.q() },
      )
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.success.set('Saved.');
        },
        error: (e: HttpErrorResponse) => {
          this.saving.set(false);
          this.error.set(errMsg(e, 'Could not save.'));
        },
      });
  }
}
