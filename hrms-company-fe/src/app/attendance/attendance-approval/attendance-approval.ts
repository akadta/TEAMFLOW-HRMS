import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { BrandService } from '../../core/brand/brand.service';
import {
  ReqRow,
  STATUS,
  band,
  dayLabel,
  dt12,
  errMsg,
  fileUrl,
  lateText,
  t12,
} from '../attendance.util';

@Component({
  selector: 'app-attendance-approval',
  templateUrl: './attendance-approval.html',
  styleUrl: '../attendance.css',
})
export class AttendanceApproval {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/company/attendance/requests`;
  readonly brand = inject(BrandService);

  readonly STATUS = STATUS;
  readonly band = band;
  readonly lateText = lateText;
  readonly dayLabel = dayLabel;
  readonly t12 = t12;
  readonly dt12 = dt12;
  readonly fileUrl = fileUrl;
  readonly tabs = [
    { id: 'pending', label: 'Pending' },
    { id: 'approved', label: 'Approved' },
    { id: 'rejected', label: 'Rejected' },
    { id: 'all', label: 'All' },
  ];

  status = signal('pending');
  from = signal('');
  to = signal('');
  search = signal('');
  rows = signal<ReqRow[]>([]);
  loading = signal(true);
  busy = signal(false);
  error = signal<string | null>(null);
  sel = signal<ReqRow | null>(null);
  note = signal('');

  shown = computed(() => {
    const q = this.search().trim().toLowerCase();
    return q
      ? this.rows().filter((r) => `${r.name} ${r.code}`.toLowerCase().includes(q))
      : this.rows();
  });

  constructor() {
    this.load();
  }

  load() {
    const params: Record<string, string> = {};
    if (this.status() !== 'all') params['status'] = this.status();
    if (this.from()) params['from'] = this.from();
    if (this.to()) params['to'] = this.to();

    this.loading.set(true);
    this.error.set(null);
    this.http.get<ReqRow[]>(this.api, { params }).subscribe({
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

  setStatus(s: string) {
    this.status.set(s);
    this.load();
  }

  val(e: Event) {
    return (e.target as HTMLInputElement).value;
  }

  open(r: ReqRow) {
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
          this.error.set(errMsg(e, 'Could not save decision.'));
        },
      });
  }
}
