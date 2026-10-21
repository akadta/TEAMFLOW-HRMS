import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { STATUS, dayLabel, dt12, errMsg, istDate, short } from '../../attendance/attendance.util';
import { BrandService } from '../../core/brand/brand.service';

interface CalDay {
  date: string;
  kind: string;
  label: string;
}

interface Mine {
  id: number;
  reason: string;
  status: string;
  createdAt: string;
  decidedAt: string | null;
  decisionNote: string | null;
  days: string[];
  files: { fileName: string; filePath: string }[];
  approvals: { group: string; mode: string; decision: string; approver: string | null }[];
}

interface Cell {
  iso: string;
  d: number;
  out: boolean;
  past: boolean;
  hit: CalDay | null;
  on: boolean;
}

const GROUP: Record<string, string> = {
  managers: 'Reporting manager',
  owner: 'Owner',
  main: 'Main team',
  selected: 'Selected',
};
const MONTHS = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
];

@Component({
  selector: 'app-leave-request',
  templateUrl: './leave-request.html',
  styleUrl: '../../attendance/attendance.css',
})
export class LeaveRequest {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/company/leave`;
  readonly brand = inject(BrandService);

  readonly STATUS = STATUS;
  readonly dt12 = dt12;
  readonly short = short;
  readonly months = MONTHS;
  readonly names = ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa'];

  active = signal(false);
  maxDays = signal<number | null>(null);
  today = signal(istDate(Date.now()));
  blocked = signal(new Map<string, CalDay>());

  open = signal(false);
  tab = signal<'new' | 'mine'>('new');
  y = signal(Number(istDate(Date.now()).slice(0, 4)));
  m = signal(Number(istDate(Date.now()).slice(5, 7)) - 1);
  selected = signal<string[]>([]);
  notice = signal<string | null>(null);

  reason = signal('');
  files = signal<File[]>([]);
  busy = signal(false);
  error = signal<string | null>(null);
  mine = signal<Mine[]>([]);

  private base = computed(
    () => Number(this.today().slice(0, 4)) * 12 + Number(this.today().slice(5, 7)) - 1,
  );
  canPrev = computed(() => this.y() * 12 + this.m() > this.base());
  canNext = computed(() => this.y() * 12 + this.m() < this.base() + 12);

  cells = computed<Cell[]>(() => {
    const y = this.y();
    const m = this.m();
    const today = this.today();
    const sel = this.selected();
    const map = this.blocked();
    const start = 1 - new Date(Date.UTC(y, m, 1)).getUTCDay();

    return Array.from({ length: 42 }, (_, i) => {
      const t = new Date(Date.UTC(y, m, start + i));
      const iso = t.toISOString().slice(0, 10);
      return {
        iso,
        d: t.getUTCDate(),
        out: t.getUTCMonth() !== m,
        past: iso < today,
        hit: map.get(iso) ?? null,
        on: sel.includes(iso),
      };
    });
  });

  constructor() {
    this.loadCal();
  }

  private loadCal() {
    this.http
      .get<{ active: boolean; maxDays: number | null; today: string; days: CalDay[] }>(
        `${this.api}/calendar`,
      )
      .subscribe({
        next: (r) => {
          this.active.set(r.active);
          this.maxDays.set(r.maxDays);
          this.today.set(r.today);
          this.blocked.set(new Map(r.days.map((d) => [d.date, d] as [string, CalDay])));
        },
        error: (e) => {
          console.error('leave calendar failed', e);
          this.active.set(false);
        },
      });
  }

  private loadMine() {
    this.http.get<Mine[]>(`${this.api}/mine`).subscribe({ next: (l) => this.mine.set(l) });
  }

  val(e: Event) {
    return (e.target as HTMLInputElement).value;
  }

  groupName(g: string) {
    return GROUP[g] ?? g;
  }

  waiting(r: Mine) {
    return r.approvals
      .filter((a) => a.decision === 'pending')
      .map((a) => `${GROUP[a.group] ?? a.group}: ${a.approver ?? '—'}`)
      .join(' · ');
  }

  show() {
    this.error.set(null);
    this.notice.set(null);
    this.tab.set('new');
    this.loadMine();
    this.open.set(true);
  }

  go(delta: number) {
    const t = new Date(Date.UTC(this.y(), this.m() + delta, 1));
    this.y.set(t.getUTCFullYear());
    this.m.set(t.getUTCMonth());
  }

  cellCls(c: Cell) {
    if (c.out) return 'opacity-25';
    if (c.past) return 'opacity-30 cursor-not-allowed';
    if (c.hit?.kind === 'holiday') return 'bg-sky-100 text-sky-700';
    if (c.hit?.kind === 'leave') return 'bg-teal-100 text-teal-700';
    if (c.hit) return 'bg-emerald-100 text-emerald-700';
    return 'hover:bg-white/10';
  }

  pickDay(c: Cell) {
    if (c.past || c.out) return;

    if (c.hit) {
      this.notice.set(
        c.hit.kind === 'holiday'
          ? `${dayLabel(c.iso)}: this day is marked as holiday (${c.hit.label}).`
          : `${dayLabel(c.iso)}: ${c.hit.label}.`,
      );
      return;
    }

    const sel = this.selected();
    if (sel.includes(c.iso)) {
      this.selected.set(sel.filter((x) => x !== c.iso));
      this.notice.set(null);
      return;
    }

    const mx = this.maxDays();
    if (mx !== null && sel.length >= mx) {
      this.notice.set(`Max ${mx} day(s) per request.`);
      return;
    }

    this.notice.set(null);
    this.selected.set([...sel, c.iso].sort());
  }

  pick(e: Event) {
    const el = e.target as HTMLInputElement;
    const add = Array.from(el.files ?? []);
    el.value = '';
    const all = [...this.files(), ...add];
    if (all.length > 5) return this.error.set('Max 5 files.');
    if (add.some((f) => f.size > 8 * 1024 * 1024)) return this.error.set('Each file: max 8 MB.');
    this.error.set(null);
    this.files.set(all);
  }

  drop(i: number) {
    this.files.update((l) => l.filter((_, x) => x !== i));
  }

  send() {
    if (this.busy()) return;
    if (this.selected().length === 0) return this.error.set('Select at least one date.');
    const r = this.reason().trim();
    if (r.length < 5) return this.error.set('Write a reason (min 5 characters).');

    const fd = new FormData();
    fd.append('reason', r);
    fd.append('dates', this.selected().join(','));
    this.files().forEach((f) => fd.append('files', f));

    this.busy.set(true);
    this.error.set(null);
    this.http.post(`${this.api}/request`, fd).subscribe({
      next: () => {
        this.busy.set(false);
        this.selected.set([]);
        this.reason.set('');
        this.files.set([]);
        this.notice.set(null);
        this.loadCal();
        this.loadMine();
        this.tab.set('mine');
      },
      error: (e: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(errMsg(e, 'Could not send the request.'));
      },
    });
  }

  cancel(id: number) {
    this.http.post(`${this.api}/mine/${id}/cancel`, {}).subscribe({
      next: () => {
        this.loadCal();
        this.loadMine();
      },
      error: (e: HttpErrorResponse) => this.error.set(errMsg(e, 'Could not cancel.')),
    });
  }
}
