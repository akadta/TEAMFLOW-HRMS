import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { dt12, errMsg } from '../../attendance/attendance.util';
import { BrandService } from '../../core/brand/brand.service';
import { SelfTask, TASK_STATUS, TASK_STATUSES } from '../task.util';

@Component({
  selector: 'app-self-tasks',
  templateUrl: './self-tasks.html',
  styleUrl: '../../attendance/attendance.css',
})
export class SelfTasks {
  private http = inject(HttpClient);
  private router = inject(Router);
  private api = `${environment.apiUrl}/company/tasks/self`;
  readonly brand = inject(BrandService);

  readonly dt12 = dt12;
  readonly TS = TASK_STATUS;
  readonly statuses = TASK_STATUSES;
  readonly chips = ['all', ...TASK_STATUSES];

  items = signal<SelfTask[]>([]);
  loading = signal(true);
  status = signal('all');
  q = signal('');
  from = signal('');
  to = signal('');
  asc = signal(false);
  dialog = signal<{ title: string; text: string; task?: SelfTask } | null>(null);

  counts = computed(() => {
    const c: Record<string, number> = { all: this.items().length };
    for (const t of this.items()) c[t.status] = (c[t.status] ?? 0) + 1;
    return c;
  });

  shown = computed(() => {
    const q = this.q().trim().toLowerCase();
    const f = this.from();
    const t = this.to();
    const list = this.items().filter((x) => {
      const d = x.createdAt.slice(0, 10);
      return (
        (this.status() === 'all' || x.status === this.status()) &&
        (!f || d >= f) &&
        (!t || d <= t) &&
        (!q || `${x.title} ${x.description ?? ''}`.toLowerCase().includes(q))
      );
    });
    return this.asc() ? [...list].reverse() : list;
  });

  constructor() {
    this.http.get<SelfTask[]>(this.api).subscribe({
      next: (l) => {
        this.items.set(l);
        this.loading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.dialog.set({
          title: 'Could not load tasks',
          text: errMsg(e, 'Could not load your tasks.'),
        });
      },
    });
  }

  val(e: Event) {
    return (e.target as HTMLInputElement).value;
  }

  add() {
    this.router.navigate(['/tasks/self/new']);
  }

  edit(t: SelfTask) {
    this.router.navigate(['/tasks/self', t.id]);
  }

  setStatus(t: SelfTask, e: Event) {
    const status = (e.target as HTMLSelectElement).value;
    this.http
      .patch<{ status: string; completedAt: string | null }>(`${this.api}/${t.id}/status`, {
        status,
      })
      .subscribe({
        next: (r) =>
          this.items.update((l) =>
            l.map((x) =>
              x.id === t.id
                ? {
                    ...x,
                    status: r.status,
                    completedAt: r.completedAt,
                    updatedAt: new Date().toISOString(),
                  }
                : x,
            ),
          ),
        error: (x: HttpErrorResponse) =>
          this.dialog.set({
            title: 'Could not update',
            text: errMsg(x, 'Could not update the status.'),
          }),
      });
  }

  askDelete(t: SelfTask) {
    this.dialog.set({
      title: 'Delete task',
      text: `Delete "${t.title}" and its files? This cannot be undone.`,
      task: t,
    });
  }

  confirmDelete() {
    const t = this.dialog()?.task;
    this.dialog.set(null);
    if (!t) return;
    this.http.delete(`${this.api}/${t.id}`).subscribe({
      next: () => this.items.update((l) => l.filter((x) => x.id !== t.id)),
      error: (x: HttpErrorResponse) =>
        this.dialog.set({
          title: 'Could not delete',
          text: errMsg(x, 'Could not delete the task.'),
        }),
    });
  }
}
