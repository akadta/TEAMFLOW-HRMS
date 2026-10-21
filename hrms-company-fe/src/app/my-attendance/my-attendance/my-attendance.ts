import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { DecimalPipe } from '@angular/common';
import {
  BANDS,
  RESULT_CLS,
  STATUS,
  band,
  dayLabel,
  dt12,
  errMsg,
  fileUrl,
  hhmm,
  istDate,
  lateText,
  mapUrl,
  result,
  srcName,
  t12,
} from '../../attendance/attendance.util';
import { BrandService } from '../../core/brand/brand.service';

interface Day {
  date: string;
  status: string;
  note: string | null;
  checkInAt: string | null;
  lateMinutes: number;
  source: string | null;
  lat: number | null;
  lng: number | null;
  photoPath: string | null;
}
interface Slot {
  date: string;
  from: string;
  to: string;
  status: string;
  at: string | null;
  lat: number | null;
  lng: number | null;
}
interface Login {
  at: string;
  lat: number;
  lng: number;
  accuracy: number | null;
}

const shiftIso = (iso: string, n: number) => {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, d + n)).toISOString().slice(0, 10);
};

@Component({
  selector: 'app-my-attendance',
  imports: [DecimalPipe],
  templateUrl: './my-attendance.html',
  styleUrl: '../../attendance/attendance.css',
})
export class MyAttendance {
  private http = inject(HttpClient);
  private today = istDate(Date.now());
  readonly brand = inject(BrandService);

  readonly STATUS = STATUS;
  readonly BANDS = BANDS;
  readonly RES = RESULT_CLS;
  readonly band = band;
  readonly lateText = lateText;
  readonly dayLabel = dayLabel;
  readonly dt12 = dt12;
  readonly t12 = t12;
  readonly hhmm = hhmm;
  readonly fileUrl = fileUrl;
  readonly mapUrl = mapUrl;
  readonly result = result;
  readonly srcName = srcName;
  readonly chips = ['all', 'present', 'absent', 'pending', 'holiday', 'leave'];
  readonly slotChips = ['all', 'done', 'missed', 'open', 'upcoming'];
  readonly slotLabel: Record<string, string> = {
    all: 'All',
    done: 'Done',
    missed: 'Missed',
    open: 'Open',
    upcoming: 'Upcoming',
  };
  readonly slotCls: Record<string, string> = {
    done: 'bg-emerald-100 text-emerald-700',
    missed: 'bg-red-100 text-red-700',
    open: 'bg-amber-100 text-amber-700',
    upcoming: 'bg-slate-100 text-slate-500',
  };
  readonly presets = [
    { id: 'month', label: 'This month' },
    { id: 'last', label: 'Last month' },
    { id: '7', label: 'Last 7 days' },
    { id: '30', label: 'Last 30 days' },
  ];

  tab = signal<'days' | 'checkins' | 'logins'>('days');
  from = signal(this.today.slice(0, 8) + '01');
  to = signal(this.today);
  rows = signal<Day[]>([]);
  slots = signal<Slot[]>([]);
  logins = signal<Login[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  status = signal('all');
  bandIdx = signal(-1);
  q = signal('');
  slotStatus = signal('all');

  counts = computed(() => {
    const c: Record<string, number> = { all: this.rows().length };
    for (const r of this.rows()) c[r.status] = (c[r.status] ?? 0) + 1;
    return c;
  });

  shown = computed(() => {
    const q = this.q().trim().toLowerCase();
    const b = this.bandIdx();
    return this.rows().filter(
      (r) =>
        (this.status() === 'all' || r.status === this.status()) &&
        (b < 0 || (!!r.checkInAt && BANDS.indexOf(band(r.lateMinutes)) === b)) &&
        (!q || (r.note ?? '').toLowerCase().includes(q)),
    );
  });

  slotCounts = computed(() => {
    const c: Record<string, number> = { all: this.slots().length };
    for (const s of this.slots()) c[s.status] = (c[s.status] ?? 0) + 1;
    return c;
  });

  slotShown = computed(() =>
    this.slots().filter((s) => this.slotStatus() === 'all' || s.status === this.slotStatus()),
  );

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
      .get<{ rows: Day[]; slots: Slot[]; logins: Login[] }>(
        `${environment.apiUrl}/company/my-attendance`,
        {
          params: { from: this.from(), to: this.to() },
        },
      )
      .subscribe({
        next: (r) => {
          this.rows.set(r.rows);
          this.slots.set(r.slots);
          this.logins.set(r.logins);
          this.loading.set(false);
        },
        error: (e: HttpErrorResponse) => {
          this.loading.set(false);
          this.error.set(errMsg(e, 'Could not load your attendance.'));
        },
      });
  }

  preset(k: string) {
    const t = this.today;
    const y = Number(t.slice(0, 4));
    const m = Number(t.slice(5, 7));

    if (k === 'month') {
      this.from.set(t.slice(0, 8) + '01');
      this.to.set(t);
    } else if (k === 'last') {
      this.from.set(new Date(Date.UTC(y, m - 2, 1)).toISOString().slice(0, 10));
      this.to.set(new Date(Date.UTC(y, m - 1, 0)).toISOString().slice(0, 10));
    } else if (k === '7') {
      this.from.set(shiftIso(t, -6));
      this.to.set(t);
    } else {
      this.from.set(shiftIso(t, -29));
      this.to.set(t);
    }

    this.load();
  }

  reset() {
    this.status.set('all');
    this.bandIdx.set(-1);
    this.q.set('');
    this.slotStatus.set('all');
  }
}
