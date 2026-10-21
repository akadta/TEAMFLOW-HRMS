import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { dt12, errMsg, fileUrl } from '../../attendance/attendance.util';
import { BrandService } from '../../core/brand/brand.service';
import { ASSIGN_STATUS, AssignItem, TASK_STATUS, fmtSize } from '../task.util';

interface Person {
  userId: number;
  code: string;
  name: string;
  role: string | null;
}

@Component({
  selector: 'app-task-inbox',
  templateUrl: './task-inbox.html',
  styleUrl: '../../attendance/attendance.css',
})
export class TaskInbox {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/company/tasks/inbox`;
  readonly brand = inject(BrandService);

  readonly AS = ASSIGN_STATUS;
  readonly TS = TASK_STATUS;
  readonly dt12 = dt12;
  readonly fileUrl = fileUrl;
  readonly fmtSize = fmtSize;
  readonly tabs = [
    { id: 'pending', label: 'Waiting for me' },
    { id: 'all', label: 'All' },
  ];

  scope = signal('pending');
  rows = signal<AssignItem[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);
  sel = signal<AssignItem | null>(null);
  busy = signal(false);

  people = signal<Person[]>([]);
  picked = signal<number[]>([]);
  q = signal('');
  role = signal('');
  note = signal('');
  declining = signal(false);

  roles = computed(() =>
    [
      ...new Set(
        this.people()
          .map((p) => p.role)
          .filter((x): x is string => !!x),
      ),
    ].sort(),
  );
  shownPeople = computed(() => {
    const q = this.q().trim().toLowerCase();
    return this.people().filter(
      (p) =>
        (!this.role() || p.role === this.role()) &&
        (!q || `${p.name} ${p.code}`.toLowerCase().includes(q)),
    );
  });

  constructor() {
    this.load();
    this.http
      .get<Person[]>(`${environment.apiUrl}/company/tasks/assign/people`)
      .subscribe({ next: (l) => this.people.set(l) });
  }

  val(e: Event) {
    return (e.target as HTMLInputElement).value;
  }

  load() {
    this.loading.set(true);
    this.http.get<AssignItem[]>(this.api, { params: { scope: this.scope() } }).subscribe({
      next: (l) => {
        this.rows.set(l);
        this.loading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(errMsg(e, 'Could not load the inbox.'));
      },
    });
  }

  setScope(s: string) {
    this.scope.set(s);
    this.load();
  }

  open(a: AssignItem) {
    this.picked.set([]);
    this.note.set('');
    this.declining.set(false);
    this.error.set(null);
    this.sel.set(a);
  }

  toggle(id: number) {
    this.picked.update((l) => (l.includes(id) ? l.filter((x) => x !== id) : [...l, id]));
  }

  tickShown() {
    const set = new Set(this.picked());
    this.shownPeople().forEach((p) => set.add(p.userId));
    this.picked.set([...set]);
  }

  private finish() {
    this.busy.set(false);
    this.sel.set(null);
    this.load();
  }

  accept() {
    const a = this.sel();
    if (!a || this.busy()) return;
    if (this.picked().length === 0) return this.error.set('Pick at least one employee.');
    this.busy.set(true);
    this.http.post(`${this.api}/${a.id}/accept`, { userIds: this.picked() }).subscribe({
      next: () => this.finish(),
      error: (e: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(errMsg(e, 'Could not send the task.'));
      },
    });
  }

  decline() {
    const a = this.sel();
    if (!a || this.busy()) return;
    this.busy.set(true);
    this.http.post(`${this.api}/${a.id}/decline`, { note: this.note().trim() || null }).subscribe({
      next: () => this.finish(),
      error: (e: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(errMsg(e, 'Could not decline.'));
      },
    });
  }
}
