import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { environment } from '../../../../environments/environment';

type Kind = 'date' | 'weekly' | 'nth';
type Filter = 'all' | Kind;
type Scope = 'global' | 'company';

interface CompanyOption {
  id: number;
  name: string;
  isActive: boolean;
}
interface Holiday {
  id: number;
  companyId: number | null;
  name: string;
  kind: Kind;
  date: string | null;
  yearly: boolean;
  weekday: number | null;
  nth: string | null;
  color: string;
  isActive: boolean;
}
interface Draft {
  id: number | null;
  name: string;
  kind: Kind;
  date: string;
  yearly: boolean;
  weekday: number;
  nth: number[];
  color: string;
}

const ist = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Kolkata' }).format(new Date());
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
const DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const FULL = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
const ORD = ['', '1st', '2nd', '3rd', '4th', '5th'];
const blank = (): Draft => ({
  id: null,
  name: '',
  kind: 'date',
  date: '',
  yearly: false,
  weekday: 6,
  nth: [2, 4],
  color: '#ef4444',
});

@Component({
  selector: 'app-attendance-settings',
  templateUrl: './attendance-settings.html',
})
export class AttendanceSettings {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/main/attendance`;

  readonly today = ist();
  readonly months = MONTHS;
  readonly days = DAYS;
  readonly full = FULL;
  readonly nths = [1, 2, 3, 4, 5];
  readonly ord = ORD;
  readonly palette = [
    '#ef4444',
    '#f97316',
    '#eab308',
    '#16a34a',
    '#06b6d4',
    '#3b82f6',
    '#8b5cf6',
    '#ec4899',
  ];
  readonly years = Array.from({ length: 11 }, (_, i) => Number(ist().slice(0, 4)) - 5 + i);
  readonly scopes: { id: Scope; label: string }[] = [
    { id: 'global', label: 'Global' },
    { id: 'company', label: 'Company-wise' },
  ];
  readonly kinds: { id: Kind; label: string }[] = [
    { id: 'date', label: 'Date' },
    { id: 'weekly', label: 'Weekly' },
    { id: 'nth', label: 'Nth weekday' },
  ];
  readonly filters: { id: Filter; label: string }[] = [
    { id: 'all', label: 'All' },
    { id: 'date', label: 'Dates' },
    { id: 'weekly', label: 'Weekly' },
    { id: 'nth', label: 'Nth' },
  ];
  readonly inp =
    'w-full rounded-lg border border-slate-200 bg-white/80 px-2.5 py-1.5 text-xs text-slate-900 outline-none transition focus:border-blue-400 focus:ring-2 focus:ring-blue-200';

  scope = signal<Scope>('global');
  companies = signal<CompanyOption[]>([]);
  companyId = signal<number | null>(null);
  rules = signal<Holiday[]>([]);
  active = signal(false);
  filter = signal<Filter>('all');
  draft = signal<Draft>(blank());
  y = signal(Number(this.today.slice(0, 4)));
  m = signal(Number(this.today.slice(5, 7)) - 1);
  saving = signal(false);
  error = signal<string | null>(null);

  ready = computed(() => this.scope() === 'global' || this.companyId() !== null);

  shown = computed(() =>
    this.rules().filter((r) => this.filter() === 'all' || r.kind === this.filter()),
  );

  cells = computed(() => {
    const y = this.y();
    const m = this.m();
    const list = this.shown().filter((r) => r.isActive);
    const start = 1 - new Date(Date.UTC(y, m, 1)).getUTCDay();
    return Array.from({ length: 42 }, (_, i) => {
      const t = new Date(Date.UTC(y, m, start + i));
      const iso = t.toISOString().slice(0, 10);
      const hits = list.filter((r) =>
        this.hit(r, t.getUTCFullYear(), t.getUTCMonth() + 1, t.getUTCDate(), t.getUTCDay()),
      );
      return {
        iso,
        d: t.getUTCDate(),
        out: t.getUTCMonth() !== m,
        today: iso === this.today,
        hits,
      };
    });
  });

  count = computed(() => this.cells().filter((c) => !c.out && c.hits.length > 0).length);

  constructor() {
    this.http.get<CompanyOption[]>(`${environment.apiUrl}/main/company-roles/companies`).subscribe({
      next: (l) => this.companies.set(l),
      error: () => this.error.set('Could not load companies.'),
    });
    this.load();
  }

  // ---------- helpers ----------
  private msg(e: HttpErrorResponse, fallback: string) {
    return e.status === 0 ? 'Cannot reach the server. Try again.' : (e.error?.message ?? fallback);
  }
  private q(): Record<string, number> {
    return this.scope() === 'company' ? { companyId: this.companyId()! } : {};
  }
  val(e: Event) {
    return (e.target as HTMLInputElement).value;
  }

  num(e: Event) {
    return Number((e.target as HTMLSelectElement).value);
  }

  private hit(r: Holiday, y: number, m: number, d: number, dow: number) {
    if (r.kind === 'weekly') return r.weekday === dow;
    if (r.kind === 'nth')
      return r.weekday === dow && (r.nth ?? '').split(',').includes(String(Math.ceil(d / 7)));
    const [hy, hm, hd] = r.date!.split('-').map(Number);
    return hm === m && hd === d && (r.yearly || hy === y);
  }

  desc(r: Holiday) {
    if (r.kind === 'weekly') return `Every ${FULL[r.weekday!]}`;
    if (r.kind === 'nth')
      return `${r
        .nth!.split(',')
        .map((n) => ORD[+n])
        .join(', ')} ${FULL[r.weekday!]}`;
    const [y, m, d] = r.date!.split('-');
    return `${+d} ${MONTHS[+m - 1].slice(0, 3)}${r.yearly ? ' · every year' : ' ' + y}`;
  }

  // ---------- month nav ----------
  go(delta: number) {
    const t = new Date(Date.UTC(this.y(), this.m() + delta, 1));
    this.y.set(t.getUTCFullYear());
    this.m.set(t.getUTCMonth());
  }

  now() {
    this.y.set(Number(this.today.slice(0, 4)));
    this.m.set(Number(this.today.slice(5, 7)) - 1);
  }

  // ---------- scope ----------
  pickScope(s: Scope) {
    this.scope.set(s);
    this.rules.set([]);
    this.active.set(false);
    this.draft.set(blank());
    this.load();
  }

  pickCompany(e: Event) {
    const v = (e.target as HTMLSelectElement).value;
    this.companyId.set(v ? Number(v) : null);
    this.rules.set([]);
    this.active.set(false);
    this.draft.set(blank());
    this.load();
  }

  private load() {
    this.error.set(null);
    if (!this.ready()) return;
    const params = this.q();

    this.http.get<Holiday[]>(`${this.api}/holidays`, { params }).subscribe({
      next: (l) => this.rules.set(l),
      error: (e: HttpErrorResponse) => this.error.set(this.msg(e, 'Could not load holidays.')),
    });
    this.http.get<{ holidaysActive: boolean }>(`${this.api}/scope`, { params }).subscribe({
      next: (s) => this.active.set(s.holidaysActive),
    });
  }

  toggleActive() {
    if (!this.ready()) return;
    const v = !this.active();
    this.http.put(`${this.api}/scope/holidays`, { active: v }, { params: this.q() }).subscribe({
      next: () => this.active.set(v),
      error: (e: HttpErrorResponse) => this.error.set(this.msg(e, 'Could not update.')),
    });
  }

  // ---------- form ----------
  setD<K extends keyof Draft>(k: K, v: Draft[K]) {
    this.draft.update((d) => ({ ...d, [k]: v }));
  }

  toggleNth(n: number) {
    this.draft.update((d) => ({
      ...d,
      nth: d.nth.includes(n) ? d.nth.filter((x) => x !== n) : [...d.nth, n].sort(),
    }));
  }

  pickDay(iso: string) {
    this.draft.update((d) => (d.kind === 'date' ? { ...d, date: iso } : { ...blank(), date: iso }));
  }

  edit(r: Holiday) {
    this.error.set(null);
    this.draft.set({
      id: r.id,
      name: r.name,
      kind: r.kind,
      date: r.date ?? '',
      yearly: r.yearly,
      weekday: r.weekday ?? 0,
      nth: (r.nth ?? '').split(',').filter(Boolean).map(Number),
      color: r.color,
    });
  }

  cancel() {
    this.draft.set(blank());
    this.error.set(null);
  }

  private body(d: Draft) {
    return {
      companyId: this.scope() === 'company' ? this.companyId() : null,
      name: d.name.trim(),
      kind: d.kind,
      date: d.kind === 'date' ? d.date || null : null,
      yearly: d.kind === 'date' && d.yearly,
      weekday: d.kind === 'date' ? null : d.weekday,
      nth: d.kind === 'nth' ? d.nth.join(',') : null,
      color: d.color,
    };
  }

  save() {
    const d = this.draft();
    if (!d.name.trim()) return this.error.set('Name is required.');
    if (d.kind === 'date' && !d.date) return this.error.set('Pick a date.');
    if (d.kind === 'nth' && d.nth.length === 0) return this.error.set('Pick at least one week.');

    this.saving.set(true);
    this.error.set(null);

    const req = d.id
      ? this.http.put<Holiday>(`${this.api}/holidays/${d.id}`, this.body(d))
      : this.http.post<Holiday>(`${this.api}/holidays`, this.body(d));

    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.draft.set(blank());
        this.load();
      },
      error: (e: HttpErrorResponse) => {
        this.saving.set(false);
        this.error.set(this.msg(e, 'Could not save holiday.'));
      },
    });
  }

  toggleRule(r: Holiday) {
    this.http.patch(`${this.api}/holidays/${r.id}/toggle`, {}).subscribe({
      next: () =>
        this.rules.update((l) =>
          l.map((x) => (x.id === r.id ? { ...x, isActive: !x.isActive } : x)),
        ),
      error: (e: HttpErrorResponse) => this.error.set(this.msg(e, 'Could not update.')),
    });
  }

  remove(r: Holiday) {
    this.http.delete(`${this.api}/holidays/${r.id}`).subscribe({
      next: () => {
        this.rules.update((l) => l.filter((x) => x.id !== r.id));
        if (this.draft().id === r.id) this.draft.set(blank());
      },
      error: (e: HttpErrorResponse) => this.error.set(this.msg(e, 'Could not delete.')),
    });
  }

  // ---------- one-click presets ----------
  preset(p: 'sun' | 'sat' | 'national') {
    const y = this.y();
    const items =
      p === 'sun'
        ? [{ name: 'Sunday', kind: 'weekly', weekday: 0, color: '#ef4444' }]
        : p === 'sat'
          ? [{ name: '2nd & 4th Saturday', kind: 'nth', weekday: 6, nth: '2,4', color: '#f97316' }]
          : [
              {
                name: 'Republic Day',
                kind: 'date',
                date: `${y}-01-26`,
                yearly: true,
                color: '#16a34a',
              },
              {
                name: 'Independence Day',
                kind: 'date',
                date: `${y}-08-15`,
                yearly: true,
                color: '#16a34a',
              },
              {
                name: 'Gandhi Jayanti',
                kind: 'date',
                date: `${y}-10-02`,
                yearly: true,
                color: '#16a34a',
              },
            ];

    const base = {
      companyId: this.scope() === 'company' ? this.companyId() : null,
      date: null,
      yearly: false,
      weekday: null,
      nth: null,
    };

    forkJoin(items.map((i) => this.http.post(`${this.api}/holidays`, { ...base, ...i }))).subscribe(
      {
        next: () => this.load(),
        error: (e: HttpErrorResponse) => this.error.set(this.msg(e, 'Could not add preset.')),
      },
    );
  }
}
