import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { dt12, errMsg, fileUrl } from '../../my-companies/mc.util';
import { ASSIGN_STATUS, AssignItem, TASK_STATUS, fmtSize, toIso } from '../assign.util';

interface CompanyOpt {
  id: number;
  name: string;
  ownerName: string;
}

@Component({
  selector: 'app-main-assign-tasks',
  templateUrl: './assign-tasks.html',
})
export class AssignTasks {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/main/assign-tasks`;

  readonly AS = ASSIGN_STATUS;
  readonly TS = TASK_STATUS;
  readonly dt12 = dt12;
  readonly fileUrl = fileUrl;
  readonly fmtSize = fmtSize;
  readonly inp =
    'rounded-lg border border-slate-200 bg-white/80 px-2.5 py-1.5 text-xs text-slate-900 outline-none transition focus:border-blue-400 focus:ring-2 focus:ring-blue-200';

  tab = signal<'new' | 'sent'>('new');
  companies = signal<CompanyOpt[]>([]);
  picked = signal<number[]>([]);
  q = signal('');

  title = signal('');
  description = signal('');
  dueAt = signal('');
  when = signal<'now' | 'later'>('now');
  sendAt = signal('');
  files = signal<File[]>([]);
  busy = signal(false);
  error = signal<string | null>(null);
  ok = signal<string | null>(null);

  sent = signal<AssignItem[]>([]);
  sentLoading = signal(true);
  sel = signal<AssignItem | null>(null);

  shownCompanies = computed(() => {
    const q = this.q().trim().toLowerCase();
    return this.companies().filter(
      (c) => !q || `${c.name} ${c.ownerName}`.toLowerCase().includes(q),
    );
  });

  constructor() {
    this.http.get<CompanyOpt[]>(`${this.api}/companies`).subscribe({
      next: (l) => this.companies.set(l),
      error: (e: HttpErrorResponse) => this.error.set(errMsg(e, 'Could not load companies.')),
    });
    this.loadSent();
  }

  val(e: Event) {
    return (e.target as HTMLInputElement).value;
  }

  loadSent() {
    this.sentLoading.set(true);
    this.http.get<AssignItem[]>(`${this.api}/mine`).subscribe({
      next: (l) => {
        this.sent.set(l);
        this.sentLoading.set(false);
      },
      error: () => this.sentLoading.set(false),
    });
  }

  toggle(id: number) {
    this.picked.update((l) => (l.includes(id) ? l.filter((x) => x !== id) : [...l, id]));
  }

  tickShown() {
    const set = new Set(this.picked());
    this.shownCompanies().forEach((c) => set.add(c.id));
    this.picked.set([...set]);
  }

  pick(e: Event) {
    const el = e.target as HTMLInputElement;
    const add = Array.from(el.files ?? []);
    el.value = '';
    if (this.files().length + add.length > 30) return this.error.set('Max 30 files.');
    this.error.set(null);
    this.files.update((l) => [...l, ...add]);
  }

  send() {
    if (this.busy()) return;
    this.ok.set(null);
    if (!this.title().trim()) return this.error.set('Task name is required.');
    if (this.picked().length === 0) return this.error.set('Pick at least one company.');
    if (this.when() === 'later' && !this.sendAt())
      return this.error.set('Pick the time to send it.');

    const fd = new FormData();
    fd.append('title', this.title().trim());
    fd.append('description', this.description());
    fd.append('dueAt', toIso(this.dueAt()));
    fd.append('sendAt', this.when() === 'later' ? toIso(this.sendAt()) : '');
    fd.append('companyIds', this.picked().join(','));
    this.files().forEach((f) => fd.append('files', f));

    this.busy.set(true);
    this.error.set(null);
    this.http.post(this.api, fd).subscribe({
      next: () => {
        this.busy.set(false);
        this.ok.set(
          'Sent to the company owner(s). They were alerted and will choose the employees.',
        );
        this.title.set('');
        this.description.set('');
        this.dueAt.set('');
        this.sendAt.set('');
        this.when.set('now');
        this.picked.set([]);
        this.files.set([]);
        this.loadSent();
        this.tab.set('sent');
      },
      error: (e: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(errMsg(e, 'Could not send the task.'));
      },
    });
  }

  cancel(a: AssignItem) {
    this.http.post(`${this.api}/${a.id}/cancel`, {}).subscribe({
      next: () => {
        this.sel.set(null);
        this.loadSent();
      },
      error: (e: HttpErrorResponse) => this.error.set(errMsg(e, 'Could not cancel.')),
    });
  }

  done(a: AssignItem) {
    return a.targets.filter((t) => t.status === 'done').length;
  }
}
