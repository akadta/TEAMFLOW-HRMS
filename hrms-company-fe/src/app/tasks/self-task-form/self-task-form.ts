import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { errMsg, fileUrl } from '../../attendance/attendance.util';
import { BrandService } from '../../core/brand/brand.service';
import { SelfTask, TASK_STATUS, TASK_STATUSES, TaskFile, fmtSize } from '../task.util';

@Component({
  selector: 'app-self-task-form',
  templateUrl: './self-task-form.html',
  styleUrl: '../../attendance/attendance.css',
})
export class SelfTaskForm {
  private http = inject(HttpClient);
  private router = inject(Router);
  private id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id') || 0);
  private api = `${environment.apiUrl}/company/tasks/self`;
  readonly brand = inject(BrandService);

  readonly isEdit = this.id > 0;
  readonly fileUrl = fileUrl;
  readonly fmtSize = fmtSize;
  readonly TS = TASK_STATUS;
  readonly statuses = TASK_STATUSES;

  title = signal('');
  description = signal('');
  status = signal('todo');
  existing = signal<TaskFile[]>([]);
  removed = signal<number[]>([]);
  added = signal<File[]>([]);

  loading = signal(this.isEdit);
  busy = signal(false);
  error = signal<string | null>(null);
  confirmDel = signal(false);

  kept = computed(() => this.existing().filter((f) => !this.removed().includes(f.id)));
  total = computed(() => this.kept().length + this.added().length);

  constructor() {
    if (this.isEdit)
      this.http.get<SelfTask>(`${this.api}/${this.id}`).subscribe({
        next: (t) => {
          this.title.set(t.title);
          this.description.set(t.description ?? '');
          this.status.set(t.status);
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
    this.router.navigate(['/tasks/self']);
  }

  pick(e: Event) {
    const el = e.target as HTMLInputElement;
    const add = Array.from(el.files ?? []);
    el.value = '';
    if (this.total() + add.length > 10) return this.error.set('Max 10 files per task.');
    if (add.some((f) => f.size > 25 * 1024 * 1024)) return this.error.set('Each file: max 25 MB.');
    this.error.set(null);
    this.added.update((l) => [...l, ...add]);
  }

  dropNew(i: number) {
    this.added.update((l) => l.filter((_, x) => x !== i));
  }

  dropOld(id: number) {
    this.removed.update((l) => [...l, id]);
  }

  save() {
    if (this.busy()) return;
    if (!this.title().trim()) return this.error.set('Task name is required.');

    const fd = new FormData();
    fd.append('title', this.title().trim());
    fd.append('description', this.description());
    fd.append('status', this.status());
    if (this.isEdit) fd.append('removeFiles', this.removed().join(','));
    this.added().forEach((f) => fd.append('files', f));

    this.busy.set(true);
    this.error.set(null);
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
