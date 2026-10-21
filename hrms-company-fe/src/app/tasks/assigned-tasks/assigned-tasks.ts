import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { dt12, errMsg, fileUrl } from '../../attendance/attendance.util';
import { BrandService } from '../../core/brand/brand.service';
import {
  APPROVAL,
  DailyTask,
  GROUP,
  TASK_STATUS,
  TASK_STATUSES,
  fmtSize,
  isImage,
  isVideo,
  overdue,
} from '../task.util';

@Component({
  selector: 'app-assigned-tasks',
  templateUrl: './assigned-tasks.html',
  styleUrl: '../../attendance/attendance.css',
})
export class AssignedTasks {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/company/tasks/assigned`;
  readonly brand = inject(BrandService);

  readonly dt12 = dt12;
  readonly fileUrl = fileUrl;
  readonly fmtSize = fmtSize;
  readonly isImage = isImage;
  readonly isVideo = isVideo;
  readonly overdue = overdue;
  readonly TS = TASK_STATUS;
  readonly AP = APPROVAL;
  readonly group = GROUP;
  readonly statuses = TASK_STATUSES;
  readonly chips = ['all', ...TASK_STATUSES];

  items = signal<DailyTask[]>([]);
  loading = signal(true);
  status = signal('all');
  q = signal('');
  late = signal(false);
  sel = signal<DailyTask | null>(null);
  dialog = signal<{ title: string; text: string } | null>(null);

  counts = computed(() => {
    const c: Record<string, number> = { all: this.items().length };
    for (const t of this.items()) c[t.status] = (c[t.status] ?? 0) + 1;
    return c;
  });

  shown = computed(() => {
    const q = this.q().trim().toLowerCase();
    return this.items().filter(
      (t) =>
        (this.status() === 'all' || t.status === this.status()) &&
        (!this.late() || overdue(t)) &&
        (!q || `${t.title} ${t.description ?? ''}`.toLowerCase().includes(q)),
    );
  });

  constructor() {
    this.http.get<DailyTask[]>(this.api).subscribe({
      next: (l) => {
        this.items.set(l);
        this.loading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.dialog.set({
          title: 'Could not load tasks',
          text: errMsg(e, 'Could not load your assigned tasks.'),
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

  private put(t: DailyTask) {
    this.items.update((l) => l.map((x) => (x.id === t.id ? t : x)));
    if (this.sel()?.id === t.id) this.sel.set(t);
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
        next: (r) => this.put(r),
        error: (x: HttpErrorResponse) => this.fail(x, 'Could not change the progress.'),
      });
  }

  complete(t: DailyTask) {
    this.http.post<DailyTask>(`${this.api}/${t.id}/complete`, {}).subscribe({
      next: (r) => this.put(r),
      error: (x: HttpErrorResponse) => this.fail(x, 'Could not complete the task.'),
    });
  }
}
