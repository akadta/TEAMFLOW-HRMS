import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { STATUS, dt12, errMsg, fileUrl, istDate, short } from '../../my-companies/mc.util';
import {
  APPROVAL,
  GROUP,
  TASK_STATUS,
  TASK_STATUSES,
  TeamTask,
  isImage,
  isVideo,
  overdue,
} from '../team.util';

interface LeaveItem {
  id: number;
  company: string | null;
  name: string;
  code: string;
  reason: string;
  status: string;
  createdAt: string;
  decidedAt: string | null;
  decisionNote: string | null;
  decidedBy: string | null;
  days: string[];
  files: { fileName: string; filePath: string }[];
  approvals: {
    id: number;
    group: string;
    mode: string;
    decision: string;
    approver: string | null;
  }[];
}

const shiftIso = (iso: string, n: number) => {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, d + n)).toISOString().slice(0, 10);
};

@Component({
  selector: 'app-team-overview',
  templateUrl: './team-overview.html',
})
export class TeamOverview {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/main/team`;
  private today = istDate(Date.now());

  readonly STATUS = STATUS;
  readonly TS = TASK_STATUS;
  readonly AP = APPROVAL;
  readonly group = GROUP;
  readonly dt12 = dt12;
  readonly short = short;
  readonly fileUrl = fileUrl;
  readonly isImage = isImage;
  readonly isVideo = isVideo;
  readonly overdue = overdue;
  readonly leaveChips = ['all', 'pending', 'approved', 'rejected', 'cancelled'];
  readonly taskChips = ['all', ...TASK_STATUSES];
  readonly inp =
    'rounded-lg border border-slate-200 bg-white/80 px-2.5 py-1.5 text-xs text-slate-900 outline-none transition focus:border-blue-400 focus:ring-2 focus:ring-blue-200';

  tab = signal<'leave' | 'tasks'>('leave');
  company = signal('');
  error = signal<string | null>(null);

  leave = signal<LeaveItem[]>([]);
  lFrom = signal('');
  lTo = signal('');
  lStatus = signal('all');
  lQ = signal('');
  lLoading = signal(true);
  selLeave = signal<LeaveItem | null>(null);

  tasks = signal<TeamTask[]>([]);
  tFrom = signal(shiftIso(this.today, -6));
  tTo = signal(this.today);
  tStatus = signal('all');
  tAppr = signal('all');
  tLate = signal(false);
  tQ = signal('');
  tLoading = signal(true);
  selTask = signal<TeamTask | null>(null);
  note = signal('');
  busy = signal(false);

  companies = computed(() =>
    [
      ...new Set(
        [...this.leave().map((r) => r.company), ...this.tasks().map((t) => t.company)].filter(
          (x): x is string => !!x,
        ),
      ),
    ].sort(),
  );

  lCounts = computed(() => {
    const c: Record<string, number> = { all: this.leave().length };
    for (const r of this.leave()) c[r.status] = (c[r.status] ?? 0) + 1;
    return c;
  });

  leaveShown = computed(() => {
    const q = this.lQ().trim().toLowerCase();
    return this.leave().filter(
      (r) =>
        (this.lStatus() === 'all' || r.status === this.lStatus()) &&
        (!this.company() || r.company === this.company()) &&
        (!q || `${r.name} ${r.code} ${r.reason}`.toLowerCase().includes(q)),
    );
  });

  tCounts = computed(() => {
    const c: Record<string, number> = { all: this.tasks().length };
    for (const t of this.tasks()) c[t.status] = (c[t.status] ?? 0) + 1;
    return c;
  });

  taskShown = computed(() => {
    const q = this.tQ().trim().toLowerCase();
    return this.tasks().filter(
      (t) =>
        (this.tStatus() === 'all' || t.status === this.tStatus()) &&
        (this.tAppr() === 'all' || t.approval === this.tAppr()) &&
        (!this.company() || t.company === this.company()) &&
        (!this.tLate() || overdue(t)) &&
        (!q || `${t.name} ${t.code ?? ''} ${t.title}`.toLowerCase().includes(q)),
    );
  });

  constructor() {
    this.loadLeave();
    this.loadTasks();
  }

  val(e: Event) {
    return (e.target as HTMLInputElement).value;
  }

  progress(r: LeaveItem) {
    return `${r.approvals.filter((a) => a.decision === 'approved').length}/${r.approvals.length}`;
  }

  loadLeave() {
    this.lLoading.set(true);
    const params: Record<string, string> = {};
    if (this.lFrom()) params['from'] = this.lFrom();
    if (this.lTo()) params['to'] = this.lTo();
    this.http.get<LeaveItem[]>(`${this.api}/leave`, { params }).subscribe({
      next: (l) => {
        this.leave.set(l);
        this.lLoading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.lLoading.set(false);
        this.error.set(errMsg(e, 'Could not load leave requests.'));
      },
    });
  }

  loadTasks() {
    this.tLoading.set(true);
    this.http
      .get<TeamTask[]>(`${this.api}/tasks`, { params: { from: this.tFrom(), to: this.tTo() } })
      .subscribe({
        next: (l) => {
          this.tasks.set(l);
          this.tLoading.set(false);
        },
        error: (e: HttpErrorResponse) => {
          this.tLoading.set(false);
          this.error.set(errMsg(e, 'Could not load tasks.'));
        },
      });
  }

  openTask(t: TeamTask) {
    this.note.set('');
    this.error.set(null);
    this.selTask.set(t);
  }

  decide(approve: boolean) {
    const t = this.selTask();
    if (!t || this.busy()) return;
    this.busy.set(true);
    this.http
      .post(`${this.api}/tasks/${t.id}/decide`, { approve, note: this.note().trim() || null })
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.selTask.set(null);
          this.loadTasks();
        },
        error: (e: HttpErrorResponse) => {
          this.busy.set(false);
          this.error.set(errMsg(e, 'Could not save the decision.'));
        },
      });
  }
}
