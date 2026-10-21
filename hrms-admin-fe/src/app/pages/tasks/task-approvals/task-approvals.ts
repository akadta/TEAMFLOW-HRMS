import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { dt12, errMsg, fileUrl } from '../../my-companies/mc.util';
import {
  APPROVAL,
  GROUP,
  TASK_STATUS,
  TeamTask,
  isImage,
  isVideo,
  overdue,
} from '../../team/team.util';

@Component({
  selector: 'app-main-task-approvals',
  templateUrl: './task-approvals.html',
})
export class TaskApprovals {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/main/task-approvals`;

  readonly TS = TASK_STATUS;
  readonly AP = APPROVAL;
  readonly group = GROUP;
  readonly dt12 = dt12;
  readonly fileUrl = fileUrl;
  readonly isImage = isImage;
  readonly isVideo = isVideo;
  readonly overdue = overdue;
  readonly inp =
    'rounded-lg border border-slate-200 bg-white/80 px-2.5 py-1.5 text-xs text-slate-900 outline-none transition focus:border-blue-400 focus:ring-2 focus:ring-blue-200';
  readonly tabs = [
    { id: 'pending', label: 'Waiting for me' },
    { id: 'all', label: 'All' },
  ];

  scope = signal('pending');
  rows = signal<TeamTask[]>([]);
  q = signal('');
  loading = signal(true);
  busy = signal(false);
  error = signal<string | null>(null);
  sel = signal<TeamTask | null>(null);
  note = signal('');

  shown = computed(() => {
    const q = this.q().trim().toLowerCase();
    return this.rows().filter(
      (t) =>
        !q || `${t.name} ${t.code ?? ''} ${t.title} ${t.company ?? ''}`.toLowerCase().includes(q),
    );
  });

  constructor() {
    this.load();
  }

  val(e: Event) {
    return (e.target as HTMLInputElement).value;
  }

  load() {
    this.loading.set(true);
    this.http.get<TeamTask[]>(this.api, { params: { scope: this.scope() } }).subscribe({
      next: (l) => {
        this.rows.set(l);
        this.loading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(errMsg(e, 'Could not load approvals.'));
      },
    });
  }

  setScope(s: string) {
    this.scope.set(s);
    this.load();
  }

  open(t: TeamTask) {
    this.note.set('');
    this.error.set(null);
    this.sel.set(t);
  }

  decide(approve: boolean) {
    const t = this.sel();
    if (!t || this.busy()) return;
    this.busy.set(true);
    this.http
      .post(`${this.api}/${t.id}/decide`, { approve, note: this.note().trim() || null })
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.sel.set(null);
          this.load();
        },
        error: (e: HttpErrorResponse) => {
          this.busy.set(false);
          this.error.set(errMsg(e, 'Could not save the decision.'));
        },
      });
  }
}
