import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { errMsg, fileUrl } from '../../attendance/attendance.util';
import { BrandService } from '../../core/brand/brand.service';
import {
  APPROVAL,
  DailyTask,
  GROUP,
  TASK_STATUS,
  TaskFile,
  fmtSize,
  toIso,
  toLocalInput,
} from '../task.util';

@Component({
  selector: 'app-daily-task-form',
  templateUrl: './daily-task-form.html',
  styleUrl: '../../attendance/attendance.css',
})
export class DailyTaskForm {
  private http = inject(HttpClient);
  private router = inject(Router);
  private id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id') || 0);
  private api = `${environment.apiUrl}/company/tasks/daily`;
  readonly brand = inject(BrandService);

  readonly isEdit = this.id > 0;
  readonly fileUrl = fileUrl;
  readonly fmtSize = fmtSize;
  readonly TS = TASK_STATUS;
  readonly AP = APPROVAL;
  readonly group = GROUP;
  readonly statuses = ['todo', 'in_progress', 'on_hold'];

  title = signal('');
  description = signal('');
  startAt = signal('');
  dueAt = signal('');
  status = signal('todo');
  progress = signal(0);
  existing = signal<TaskFile[]>([]);
  removed = signal<number[]>([]);
  added = signal<File[]>([]);
  task = signal<DailyTask | null>(null);

  loading = signal(this.isEdit);
  busy = signal(false);
  error = signal<string | null>(null);
  confirmDel = signal(false);

  kept = computed(() => this.existing().filter((f) => !this.removed().includes(f.id)));
  locked = computed(
    () => this.task()?.approval === 'pending' || this.task()?.approval === 'approved',
  );

  constructor() {
    if (this.isEdit)
      this.http.get<DailyTask>(`${this.api}/${this.id}`).subscribe({
        next: (t) => {
          this.task.set(t);
          this.title.set(t.title);
          this.description.set(t.description ?? '');
          this.startAt.set(toLocalInput(t.startAt));
          this.dueAt.set(toLocalInput(t.dueAt));
          this.status.set(t.status === 'done' ? 'in_progress' : t.status);
          this.progress.set(t.progress);
          this.existing.set(t.files);
          this.loading.set(false);
        },
        error: (e: HttpErrorResponse) => {
          this.loading.set(false);
          this.error.set(errMsg(e, 'Could not load the task.'));
        },
      });
  }

  val(e: Event) {
    return (e.target as HTMLInputElement).value;
  }

  back() {
    this.router.navigate(['/tasks/daily']);
  }

  pick(e: Event) {
    const el = e.target as HTMLInputElement;
    const add = Array.from(el.files ?? []);
    el.value = '';
    if (this.kept().length + this.added().length + add.length > 30)
      return this.error.set('Max 30 files per task.');
    this.error.set(null);
    this.added.update((l) => [...l, ...add]);
  }

  private form(complete: boolean) {
    const fd = new FormData();
    fd.append('title', this.title().trim());
    fd.append('description', this.description());
    fd.append('startAt', toIso(this.startAt()));
    fd.append('dueAt', toIso(this.dueAt()));
    fd.append('status', complete ? 'done' : this.status());
    fd.append('progress', String(complete ? 100 : this.progress()));
    if (this.isEdit) fd.append('removeFiles', this.removed().join(','));
    this.added().forEach((f) => fd.append('files', f));
    return fd;
  }

  save(complete = false) {
    if (this.busy()) return;
    if (!this.title().trim()) return this.error.set('Task name is required.');
    if (this.startAt() && this.dueAt() && this.dueAt() < this.startAt())
      return this.error.set('Due time is before the start time.');

    this.busy.set(true);
    this.error.set(null);
    const fd = this.form(complete);
    const req = this.isEdit
      ? this.http.put(`${this.api}/${this.id}`, fd)
      : this.http.post(this.api, fd);
    req.subscribe({
      next: () => this.back(),
      error: (e: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(errMsg(e, 'Could not save the task.'));
      },
    });
  }

  remove() {
    this.confirmDel.set(false);
    this.http.delete(`${this.api}/${this.id}`).subscribe({
      next: () => this.back(),
      error: (e: HttpErrorResponse) => this.error.set(errMsg(e, 'Could not delete the task.')),
    });
  }
}
