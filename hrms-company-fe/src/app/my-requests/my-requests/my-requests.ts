import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import {
  STATUS,
  band,
  dayLabel,
  dt12,
  errMsg,
  fileUrl,
  lateText,
  short,
} from '../../attendance/attendance.util';
import { BrandService } from '../../core/brand/brand.service';

type Files = { fileName: string; filePath: string }[];

interface LeaveItem {
  id: number;
  reason: string;
  status: string;
  createdAt: string;
  decidedAt: string | null;
  decisionNote: string | null;
  decidedBy: string | null;
  days: string[];
  files: Files;
  approvals: { group: string; mode: string; decision: string; approver: string | null }[];
}
interface AttItem {
  id: number;
  date: string;
  reason: string;
  status: string;
  lateMinutes: number;
  requestedAt: string;
  decidedAt: string | null;
  decisionNote: string | null;
  decidedBy: string | null;
  files: Files;
}

const GROUP: Record<string, string> = {
  managers: 'Reporting manager',
  owner: 'Owner',
  main: 'Main team',
  selected: 'Selected',
};

@Component({
  selector: 'app-my-requests',
  templateUrl: './my-requests.html',
  styleUrl: '../../attendance/attendance.css',
})
export class MyRequests {
  private http = inject(HttpClient);
  readonly brand = inject(BrandService);

  readonly STATUS = STATUS;
  readonly band = band;
  readonly lateText = lateText;
  readonly dayLabel = dayLabel;
  readonly dt12 = dt12;
  readonly short = short;
  readonly fileUrl = fileUrl;
  readonly group = GROUP;
  readonly leaveChips = ['all', 'pending', 'approved', 'rejected', 'cancelled'];
  readonly attChips = ['all', 'pending', 'approved', 'rejected'];

  tab = signal<'leave' | 'att'>('leave');
  leave = signal<LeaveItem[]>([]);
  att = signal<AttItem[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  status = signal('all');
  from = signal('');
  to = signal('');
  q = signal('');
  asc = signal(false);

  selLeave = signal<LeaveItem | null>(null);
  selAtt = signal<AttItem | null>(null);

  chips = computed(() => (this.tab() === 'leave' ? this.leaveChips : this.attChips));

  counts = computed(() => {
    const list: { status: string }[] = this.tab() === 'leave' ? this.leave() : this.att();
    const c: Record<string, number> = { all: list.length };
    for (const r of list) c[r.status] = (c[r.status] ?? 0) + 1;
    return c;
  });

  private inRange(days: string[]) {
    const f = this.from();
    const t = this.to();
    return days.some((d) => (!f || d >= f) && (!t || d <= t));
  }

  leaveShown = computed(() => {
    const st = this.status();
    const q = this.q().trim().toLowerCase();
    const l = this.leave().filter(
      (r) =>
        (st === 'all' || r.status === st) &&
        this.inRange(r.days) &&
        (!q || r.reason.toLowerCase().includes(q)),
    );
    return this.asc() ? [...l].reverse() : l;
  });

  attShown = computed(() => {
    const st = this.status();
    const q = this.q().trim().toLowerCase();
    const l = this.att().filter(
      (r) =>
        (st === 'all' || r.status === st) &&
        this.inRange([r.date]) &&
        (!q || r.reason.toLowerCase().includes(q)),
    );
    return this.asc() ? [...l].reverse() : l;
  });

  constructor() {
    this.http.get<LeaveItem[]>(`${environment.apiUrl}/company/leave/mine`).subscribe({
      next: (l) => {
        this.leave.set(l);
        this.loading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(errMsg(e, 'Could not load your requests.'));
      },
    });
    this.http.get<AttItem[]>(`${environment.apiUrl}/company/attendance/my-requests`).subscribe({
      next: (l) => this.att.set(l),
      error: () => this.att.set([]),
    });
  }

  val(e: Event) {
    return (e.target as HTMLInputElement).value;
  }

  setTab(t: 'leave' | 'att') {
    this.tab.set(t);
    this.status.set('all');
  }

  reset() {
    this.status.set('all');
    this.from.set('');
    this.to.set('');
    this.q.set('');
    this.asc.set(false);
  }

  waiting(r: LeaveItem) {
    return r.approvals
      .filter((a) => a.decision === 'pending')
      .map((a) => `${GROUP[a.group] ?? a.group}: ${a.approver ?? '—'}`)
      .join(' · ');
  }
}
