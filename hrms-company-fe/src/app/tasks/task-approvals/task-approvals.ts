import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { dt12, errMsg, fileUrl } from '../../attendance/attendance.util';
import { BrandService } from '../../core/brand/brand.service';
import { APPROVAL, DailyTask, GROUP, TASK_STATUS, isImage, isVideo, overdue } from '../task.util';

@Component({
  selector: 'app-task-approvals',
  templateUrl: './task-approvals.html',
  styleUrl: '../../attendance/attendance.css',
})
export class TaskApprovals {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/company/tasks/approvals`;
  readonly brand = inject(BrandService);

  readonly TS = TASK_STATUS;
  readonly AP = APPROVAL;
  readonly group = GROUP;
  readonly dt12 = dt12;
  readonly fileUrl = fileUrl;
  readonly isImage = isImage;
  readonly isVideo = isVideo;
  readonly overdue = overdue;
  readonly tabs = [
    { id: 'pending', label: 'Waiting for me' },
    { id: 'all', label: 'All' },
  ];

  scope = signal('pending');
  rows = signal<DailyTask[]>([]);
  q = signal('');
  loading = signal(true);
  busy = signal(false);
  error = signal<string | null>(null);
  sel = signal<DailyTask | null>(null);
  note = signal('');

  shown = computed(() => {
    const q = this.q().trim().toLowerCase();
    return this.rows().filter(
      (t) => !q || `${t.name} ${t.code ?? ''} ${t.title}`.toLowerCase().includes(q),
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
    this.http.get<DailyTask[]>(this.api, { params: { scope: this.scope() } }).subscribe({
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

  open(t: DailyTask) {
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
