import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { STATUS, dt12, errMsg, fileUrl, short } from '../../attendance/attendance.util';
import { BrandService } from '../../core/brand/brand.service';

interface Item {
  id: number;
  company: string;
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
    note: string | null;
  }[];
  canDecide: boolean;
}

const GROUP: Record<string, string> = {
  managers: 'Reporting manager',
  owner: 'Owner',
  main: 'Main team',
  selected: 'Selected',
};

@Component({
  selector: 'app-leave-approvals',
  templateUrl: './leave-approvals.html',
  styleUrl: '../../attendance/attendance.css',
})
export class LeaveApprovals {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/company/leave/approvals`;
  readonly brand = inject(BrandService);

  readonly STATUS = STATUS;
  readonly dt12 = dt12;
  readonly short = short;
  readonly fileUrl = fileUrl;
  readonly group = GROUP;
  readonly tabs = [
    { id: 'pending', label: 'Pending' },
    { id: 'all', label: 'All' },
  ];

  scope = signal('pending');
  rows = signal<Item[]>([]);
  loading = signal(true);
  busy = signal(false);
  error = signal<string | null>(null);
  sel = signal<Item | null>(null);
  note = signal('');

  constructor() {
    this.load();
  }

  val(e: Event) {
    return (e.target as HTMLInputElement).value;
  }

  progress(r: Item) {
    const done = r.approvals.filter((a) => a.decision === 'approved').length;
    return `${done}/${r.approvals.length}`;
  }

  load() {
    this.loading.set(true);
    this.http.get<Item[]>(this.api, { params: { scope: this.scope() } }).subscribe({
      next: (l) => {
        this.rows.set(l);
        this.loading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(errMsg(e, 'Could not load requests.'));
      },
    });
  }

  setScope(s: string) {
    this.scope.set(s);
    this.load();
  }

  open(r: Item) {
    this.note.set('');
    this.error.set(null);
    this.sel.set(r);
  }

  decide(approve: boolean) {
    const r = this.sel();
    if (!r || this.busy()) return;

    this.busy.set(true);
    this.error.set(null);
    this.http
      .post(`${this.api}/${r.id}/decide`, { approve, note: this.note().trim() || null })
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
