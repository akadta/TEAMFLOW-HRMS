import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { environment } from '../../../../environments/environment';

type Scope = 'global' | 'company' | 'employee';

interface CompanyOption {
  id: number;
  name: string;
  isActive: boolean;
}
interface RoleOption {
  id: number;
  name: string;
  isActive: boolean;
}
interface EmpOption {
  userId: number;
  code: string;
  name: string;
  role: string | null;
  hasOverride: boolean;
  overrideActive: boolean;
}
interface Slot {
  from: string;
  to: string;
}
interface Row {
  day: number;
  off: boolean;
  custom: boolean;
  from: string;
  to: string;
  grace: string;
}
interface Policy {
  active: boolean;
  timeFrom: string;
  timeTo: string;
  graceUntil: string | null;
  rules: {
    day: number;
    off: boolean;
    from: string | null;
    to: string | null;
    grace: string | null;
  }[];
  ownerDays: number[];
  requireLocation: boolean;
  requirePhoto: boolean;
  loginLocation: boolean;
  periods: Slot[];
  hasOwn: boolean;
}

const FULL = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
const TIME = /^([01]\d|2[0-3]):[0-5]\d$/;
const rowsOf = (): Row[] =>
  Array.from({ length: 7 }, (_, day) => ({
    day,
    off: false,
    custom: false,
    from: '09:00',
    to: '18:00',
    grace: '',
  }));
const mins = (f: string, t: string) => {
  const [a, b] = f.split(':').map(Number);
  const [c, d] = t.split(':').map(Number);
  return (c * 60 + d - (a * 60 + b) + 1440) % 1440;
};

@Component({
  selector: 'app-attendance-timing',
  templateUrl: './attendance-timing.html',
})
export class AttendanceTiming {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/main/attendance-policy`;
  private timer?: ReturnType<typeof setTimeout>;

  readonly full = FULL;
  readonly scopes: { id: Scope; label: string }[] = [
    { id: 'global', label: 'Global' },
    { id: 'company', label: 'Company-wise' },
    { id: 'employee', label: 'Employee-wise' },
  ];
  readonly inp =
    'w-full rounded-lg border border-slate-200 bg-white/80 px-2.5 py-1.5 text-xs text-slate-900 outline-none transition focus:border-blue-400 focus:ring-2 focus:ring-blue-200';

  scope = signal<Scope>('global');
  companies = signal<CompanyOption[]>([]);
  companyId = signal<number | null>(null);
  roles = signal<RoleOption[]>([]);
  roleId = signal('');
  search = signal('');
  emps = signal<EmpOption[]>([]);
  userId = signal<number | null>(null);
  hasOwn = signal(false);

  active = signal(false);
  from = signal('09:00');
  to = signal('18:00');
  grace = signal('');
  ownerDays = signal<number[]>([]);
  rows = signal<Row[]>(rowsOf());
  reqLoc = signal(false);
  reqPhoto = signal(false);
  loginLoc = signal(false);
  slots = signal<Slot[]>([]);
  newFrom = signal('11:00');
  newTo = signal('11:30');

  saving = signal(false);
  error = signal<string | null>(null);
  success = signal<string | null>(null);

  ready = computed(() => {
    const s = this.scope();
    return (
      s === 'global' ||
      (s === 'company' && this.companyId() !== null) ||
      (s === 'employee' && this.companyId() !== null && this.userId() !== null)
    );
  });

  week = computed(() =>
    this.rows().reduce(
      (s, r) => (r.off ? s : s + (r.custom ? mins(r.from, r.to) : mins(this.from(), this.to()))),
      0,
    ),
  );

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
    const s = this.scope();
    if (s === 'company') return { companyId: this.companyId()! };
    if (s === 'employee') return { companyId: this.companyId()!, userId: this.userId()! };
    return {};
  }

  val(e: Event) {
    return (e.target as HTMLInputElement).value;
  }

  fmt(t: string) {
    if (!TIME.test(t)) return '—';
    const [h, m] = t.split(':').map(Number);
    return `${h % 12 || 12}:${String(m).padStart(2, '0')} ${h < 12 ? 'AM' : 'PM'}`;
  }

  hm(n: number) {
    return `${Math.floor(n / 60)}h${n % 60 ? ' ' + (n % 60) + 'm' : ''}`;
  }

  gmin(f: string, g: string) {
    return g && TIME.test(g) ? mins(f, g) : 0;
  }

  span(r: Row) {
    return r.custom ? mins(r.from, r.to) : mins(this.from(), this.to());
  }

  private graceBad(f: string, t: string, g: string) {
    return g !== '' && (!TIME.test(g) || mins(f, g) <= 0 || mins(f, g) >= mins(f, t));
  }

  // ---------- scope ----------
  pickScope(s: Scope) {
    this.scope.set(s);
    this.userId.set(null);
    this.reset();
    if (s === 'employee' && this.companyId() !== null) this.loadPeople();
    this.load();
  }

  pickCompany(e: Event) {
    const v = (e.target as HTMLSelectElement).value;
    this.companyId.set(v ? Number(v) : null);
    this.userId.set(null);
    this.roleId.set('');
    this.search.set('');
    this.emps.set([]);
    this.reset();
    if (this.scope() === 'employee' && this.companyId() !== null) this.loadPeople();
    this.load();
  }

  private loadPeople() {
    const cid = this.companyId()!;
    this.http
      .get<RoleOption[]>(`${environment.apiUrl}/main/company-roles`, { params: { companyId: cid } })
      .subscribe({ next: (l) => this.roles.set(l.filter((r) => r.isActive)) });
    this.loadEmps();
  }

  private loadEmps() {
    const params: Record<string, string | number> = { companyId: this.companyId()! };
    if (this.roleId()) params['roleId'] = Number(this.roleId());
    if (this.search().trim()) params['search'] = this.search().trim();
    this.http.get<EmpOption[]>(`${this.api}/employees`, { params }).subscribe({
      next: (l) => this.emps.set(l),
      error: (e: HttpErrorResponse) => this.error.set(this.msg(e, 'Could not load employees.')),
    });
  }

  pickRole(e: Event) {
    this.roleId.set((e.target as HTMLSelectElement).value);
    this.loadEmps();
  }

  onSearch(e: Event) {
    this.search.set(this.val(e));
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.loadEmps(), 300);
  }

  pickEmp(id: number) {
    this.userId.set(id);
    this.reset();
    this.load();
  }

  private reset() {
    this.active.set(false);
    this.from.set('09:00');
    this.to.set('18:00');
    this.grace.set('');
    this.ownerDays.set([]);
    this.rows.set(rowsOf());
    this.reqLoc.set(false);
    this.reqPhoto.set(false);
    this.loginLoc.set(false);
    this.slots.set([]);
    this.hasOwn.set(false);
    this.error.set(null);
    this.success.set(null);
  }

  private load() {
    if (!this.ready()) return;
    this.http.get<Policy>(this.api, { params: this.q() }).subscribe({
      next: (s) => {
        this.active.set(s.active);
        this.from.set(s.timeFrom);
        this.to.set(s.timeTo);
        this.grace.set(s.graceUntil ?? '');
        this.ownerDays.set(s.ownerDays);
        this.reqLoc.set(s.requireLocation);
        this.reqPhoto.set(s.requirePhoto);
        this.loginLoc.set(s.loginLocation);
        this.slots.set(s.periods);
        this.hasOwn.set(s.hasOwn);
        this.rows.set(
          rowsOf().map((r) => {
            const x = s.rules.find((z) => z.day === r.day);
            return x
              ? {
                  ...r,
                  off: x.off,
                  custom: !x.off && !!x.from,
                  from: x.from ?? s.timeFrom,
                  to: x.to ?? s.timeTo,
                  grace: x.grace ?? '',
                }
              : { ...r, from: s.timeFrom, to: s.timeTo };
          }),
        );
      },
      error: (e: HttpErrorResponse) => this.error.set(this.msg(e, 'Could not load settings.')),
    });
  }

  // ---------- rows ----------
  patch(day: number, p: Partial<Row>) {
    this.rows.update((l) => l.map((r) => (r.day === day ? { ...r, ...p } : r)));
  }

  toggleCustom(r: Row) {
    this.patch(
      r.day,
      r.custom
        ? { custom: false }
        : { custom: true, from: this.from(), to: this.to(), grace: this.grace() },
    );
  }

  plan(offDays: number[]) {
    this.rows.update((l) => l.map((r) => ({ ...r, off: offDays.includes(r.day) })));
  }

  toggleOwnerAll() {
    this.ownerDays.set(this.ownerDays().length === 7 ? [] : [0, 1, 2, 3, 4, 5, 6]);
  }

  toggleOwnerDay(d: number) {
    this.ownerDays.update((l) => (l.includes(d) ? l.filter((x) => x !== d) : [...l, d].sort()));
  }

  // ---------- check-in windows ----------
  addSlot() {
    const f = this.newFrom();
    const t = this.newTo();
    if (!TIME.test(f) || !TIME.test(t) || f >= t)
      return this.error.set('Check-in window: To must be after From.');
    if (this.slots().some((s) => f < s.to && t > s.from))
      return this.error.set(
        'Windows cannot overlap. Start the next window at or after the previous one ends.',
      );
    if (this.slots().length >= 12) return this.error.set('Max 12 check-in windows.');

    this.error.set(null);
    this.slots.update((l) =>
      [...l, { from: f, to: t }].sort((a, b) => a.from.localeCompare(b.from)),
    );

    // next suggestion: starts where this one ends, 30 minutes long
    const m = Math.min(23 * 60 + 59, Number(t.slice(0, 2)) * 60 + Number(t.slice(3, 5)) + 30);
    this.newFrom.set(t);
    this.newTo.set(
      `${String(Math.floor(m / 60)).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}`,
    );
  }

  removeSlot(i: number) {
    this.slots.update((l) => l.filter((_, x) => x !== i));
  }

  // ---------- save / remove ----------
  private fail(m: string) {
    this.success.set(null);
    this.error.set(m);
  }

  save() {
    if (this.saving() || !this.ready()) return;

    const f = this.from();
    const t = this.to();
    if (!TIME.test(f) || !TIME.test(t)) return this.fail('Enter valid From and To time.');
    if (f === t) return this.fail('From and To cannot be same.');
    if (this.graceBad(f, t, this.grace()))
      return this.fail('Grace time must be after From and before To.');

    for (const r of this.rows()) {
      if (r.custom && !r.off && (!TIME.test(r.from) || !TIME.test(r.to) || r.from === r.to))
        return this.fail(`${FULL[r.day]}: invalid time.`);
      if (r.custom && !r.off && this.graceBad(r.from, r.to, r.grace))
        return this.fail(`${FULL[r.day]}: grace must be after From and before To.`);
    }

    const rules = this.rows()
      .filter((r) => r.off || r.custom)
      .map((r) => ({
        day: r.day,
        off: r.off,
        from: r.off ? null : r.from,
        to: r.off ? null : r.to,
        grace: r.off ? null : r.grace || null,
      }));

    this.saving.set(true);
    this.error.set(null);
    this.success.set(null);

    this.http
      .put(
        this.api,
        {
          active: this.active(),
          timeFrom: f,
          timeTo: t,
          graceUntil: this.grace() || null,
          rules,
          ownerDays: this.ownerDays(),
          requireLocation: this.reqLoc(),
          requirePhoto: this.reqPhoto(),
          loginLocation: this.loginLoc(),
          periods: this.slots(),
        },
        { params: this.q() },
      )
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.success.set('Saved.');
          this.hasOwn.set(true);
          if (this.scope() === 'employee') this.loadEmps();
        },
        error: (e: HttpErrorResponse) => {
          this.saving.set(false);
          this.fail(this.msg(e, 'Could not save.'));
        },
      });
  }

  removeOwn() {
    if (
      !confirm("Remove this employee's own settings? They will follow the company settings again.")
    )
      return;
    this.http.delete(this.api, { params: this.q() }).subscribe({
      next: () => {
        this.reset();
        this.load();
        this.loadEmps();
      },
      error: (e: HttpErrorResponse) => this.fail(this.msg(e, 'Could not remove.')),
    });
  }
}
