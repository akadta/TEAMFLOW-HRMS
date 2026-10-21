import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { BrandService } from '../../core/brand/brand.service';
import { dayLabel, errMsg, hhmm, istDate, t12 } from '../attendance.util';

interface Row {
  date: string;
  userId: number;
  code: string;
  name: string;
  role: string | null;
  from: string;
  to: string;
  status: string;
  at: string | null;
  lat: number | null;
  lng: number | null;
}

const PAGE = 50;

@Component({
  selector: 'app-attendance-checkins',
  templateUrl: './attendance-checkins.html',
  styleUrl: '../attendance.css',
})
export class AttendanceCheckins {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/company/attendance/checkins`;
  private today = istDate(Date.now());
  readonly brand = inject(BrandService);

  readonly dayLabel = dayLabel;
  readonly hhmm = hhmm;
  readonly t12 = t12;
  readonly chips = ['all', 'done', 'missed', 'open', 'upcoming'];
  readonly label: Record<string, string> = {
    all: 'All',
    done: 'Done',
    missed: 'Missed',
    open: 'Open',
    upcoming: 'Upcoming',
  };
  readonly cls: Record<string, string> = {
    done: 'bg-emerald-100 text-emerald-700',
    missed: 'bg-red-100 text-red-700',
    open: 'bg-amber-100 text-amber-700',
    upcoming: 'bg-slate-100 text-slate-500',
  };

  from = signal(this.today.slice(0, 8) + '01');
  to = signal(this.today);
  rows = signal<Row[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  search = signal('');
  role = signal('');
  status = signal('all');
  page = signal(0);

  roles = computed(() =>
    [
      ...new Set(
        this.rows()
          .map((r) => r.role)
          .filter((x): x is string => !!x),
      ),
    ].sort(),
  );

  counts = computed(() => {
    const c: Record<string, number> = { all: this.rows().length };
    for (const r of this.rows()) c[r.status] = (c[r.status] ?? 0) + 1;
    return c;
  });

  filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.rows().filter(
      (r) =>
        (this.status() === 'all' || r.status === this.status()) &&
        (!this.role() || r.role === this.role()) &&
        (!q || `${r.name} ${r.code}`.toLowerCase().includes(q)),
    );
  });

  pages = computed(() => Math.max(1, Math.ceil(this.filtered().length / PAGE)));
  cur = computed(() => Math.min(this.page(), this.pages() - 1));
  pageRows = computed(() => this.filtered().slice(this.cur() * PAGE, this.cur() * PAGE + PAGE));

  constructor() {
    this.load();
  }

  val(e: Event) {
    return (e.target as HTMLInputElement).value;
  }

  load() {
    this.loading.set(true);
    this.error.set(null);
    this.http
      .get<{ rows: Row[] }>(this.api, { params: { from: this.from(), to: this.to() } })
      .subscribe({
        next: (r) => {
          this.rows.set(r.rows);
          this.loading.set(false);
        },
        error: (e: HttpErrorResponse) => {
          this.loading.set(false);
          this.rows.set([]);
          this.error.set(errMsg(e, 'Could not load the check-in report.'));
        },
      });
  }

  reset() {
    this.search.set('');
    this.role.set('');
    this.status.set('all');
    this.page.set(0);
  }
}
