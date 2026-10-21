import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { dt12, errMsg } from '../../attendance/attendance.util';
import { BrandService } from '../../core/brand/brand.service';
import { APPROVAL, DailyTask, TASK_STATUS, TASK_STATUSES, overdue, GROUP } from '../task.util';

@Component({
  selector: 'app-daily-tasks',
  templateUrl: './daily-tasks.html',
  styleUrl: '../../attendance/attendance.css',
})
export class DailyTasks {
  private http = inject(HttpClient);
  private router = inject(Router);
  private api = `${environment.apiUrl}/company/tasks/daily`;
  readonly brand = inject(BrandService);

  readonly dt12 = dt12;
  readonly overdue = overdue;
  readonly TS = TASK_STATUS;
  readonly AP = APPROVAL;
  readonly statuses = TASK_STATUSES;
  readonly chips = ['all', ...TASK_STATUSES];

  items = signal<DailyTask[]>([]);
  loading = signal(true);
  status = signal('all');
  appr = signal('all');
  q = signal('');
  from = signal('');
  to = signal('');
  late = signal(false);
  dialog = signal<{ title: string; text: string; del?: DailyTask } | null>(null);
  readonly group = GROUP;
  policy = signal<{
    required: boolean;
    mode: string;
    approvers: { group: string; name: string }[];
  } | null>(null);

  counts = computed(() => {
    const c: Record<string, number> = { all: this.items().length };
    for (const t of this.items()) c[t.status] = (c[t.status] ?? 0) + 1;
    return c;
  });

  shown = computed(() => {
    const q = this.q().trim().toLowerCase();
    return this.items().filter((t) => {
      const d = (t.startAt ?? t.createdAt).slice(0, 10);
      return (
        (this.status() === 'all' || t.status === this.status()) &&
        (this.appr() === 'all' || t.approval === this.appr()) &&
        (!this.late() || overdue(t)) &&
        (!this.from() || d >= this.from()) &&
        (!this.to() || d <= this.to()) &&
        (!q || `${t.title} ${t.description ?? ''}`.toLowerCase().includes(q))
      );
    });
  });

  constructor() {
    this.http
      .get<{ required: boolean; mode: string; approvers: { group: string; name: string }[] }>(
        `${this.api}/policy`,
      )
      .subscribe({ next: (p) => this.policy.set(p), error: () => {} });
    this.http.get<DailyTask[]>(this.api).subscribe({
      next: (l) => {
        this.items.set(l);
        this.loading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.dialog.set({
          title: 'Could not load tasks',
          text: errMsg(e, 'Could not load your daily tasks.'),
        });
      },
    });
  }

  val(e: Event) {
    return (e.target as HTMLInputElement).value;
  }

  locked(t: DailyTask) {
    return t.approval === 'pending' || t.approval === 'approved';
  }

  add() {
    this.router.navigate(['/tasks/daily/new']);
  }

  edit(t: DailyTask) {
    this.router.navigate(['/tasks/daily', t.id]);
  }

  private put(t: DailyTask) {
    this.items.update((l) => l.map((x) => (x.id === t.id ? t : x)));
    if (t.approval === 'pending')
      this.dialog.set({
        title: 'Sent for approval',
        text: 'Your task is complete and waiting for your approvers.',
      });
  }

  private fail(e: HttpErrorResponse, fallback: string) {
    this.dialog.set({ title: 'Could not update', text: errMsg(e, fallback) });
  }

  setStatus(t: DailyTask, e: Event) {
    this.http
      .patch<DailyTask>(`${this.api}/${t.id}/progress`, {
        status: (e.target as HTMLSelectElement).value,
      })
      .subscribe({
        next: (r) => this.put(r),
        error: (x: HttpErrorResponse) => this.fail(x, 'Could not change the status.'),
      });
  }

  setProgress(t: DailyTask, e: Event) {
    this.http
      .patch<DailyTask>(`${this.api}/${t.id}/progress`, {
        progress: Number((e.target as HTMLInputElement).value),
      })
      .subscribe({
        next: (r) => {
          this.put(r);
          if (r.progress === 100 && r.status !== 'done')
            this.dialog.set({
              title: 'Progress is 100%',
              text:
                'Click Complete to finish this task' +
                (this.policy()?.required ? ' and send it for approval.' : '.'),
            });
        },
        error: (x: HttpErrorResponse) => this.fail(x, 'Could not change the progress.'),
      });
  }

  complete(t: DailyTask) {
    this.http.post<DailyTask>(`${this.api}/${t.id}/complete`, {}).subscribe({
      next: (r) => this.put(r),
      error: (x: HttpErrorResponse) => this.fail(x, 'Could not complete the task.'),
    });
  }

  askDelete(t: DailyTask) {
    this.dialog.set({
      title: 'Delete task',
      text: `Delete "${t.title}" and its files? This cannot be undone.`,
      del: t,
    });
  }

  confirmDelete() {
    const t = this.dialog()?.del;
    this.dialog.set(null);
    if (!t) return;
    this.http.delete(`${this.api}/${t.id}`).subscribe({
      next: () => this.items.update((l) => l.filter((x) => x.id !== t.id)),
      error: (x: HttpErrorResponse) => this.fail(x, 'Could not delete the task.'),
    });
  }
}
