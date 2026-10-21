import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, computed, effect, inject, signal, untracked } from '@angular/core';
import { AttendanceCard } from '../../attendance/attendance-card/attendance-card';
import { dt12 } from '../../attendance/attendance.util';
import { AuthService } from '../../core/auth/auth.service';
import { BrandService } from '../../core/brand/brand.service';
import { ChartBox } from '../../core/chart/chart-box';
import { NotifyService } from '../../core/notify/notify.service';
import { environment } from '../../../environments/environment';
import { BAND_COLORS, BAND_LABELS, C, bars, donut, lines, shortDate, stack } from './dash.util';

interface Kpi {
  label: string;
  value: string | number;
  sub: string;
  cls: string;
}

interface TrendDay {
  date: string;
  present: number;
  absent: number;
  leave: number;
  holiday: number;
  late: number;
}
interface TaskTrend {
  date: string;
  created: number;
  done: number;
}

interface MeData {
  days: number;
  present: number;
  absent: number;
  leave: number;
  late: number;
  rate: number | null;
  streak: number;
  series: { date: string; status: string; late: number }[];
  pending: { attendance: number; leave: number };
  upcomingLeave: string[];
  tasks: {
    byStatus: Record<string, number>;
    open: number;
    overdue: number;
    waiting: number;
    trend: TaskTrend[];
  };
}

interface TeamData {
  available?: boolean;
  people: number;
  trend: TrendDay[];
  lateBands: number[];
  byRole: { role: string; people: number; present: number; absent: number; leave: number }[];
  requests: { attendance: number; leave: number; taskApprovals: number };
  leave7: { date: string; n: number }[];
  onLeaveToday: string[];
  absentToday: { name: string; code: string }[];
  tasks: {
    byStatus: Record<string, number>;
    overdue: number;
    waiting: number;
    avgProgress: number;
    trend: TaskTrend[];
    overdueList: { id: number; title: string; name: string; dueAt: string; progress: number }[];
  };
}

@Component({
  selector: 'app-dashboard',
  imports: [AttendanceCard, ChartBox],
  templateUrl: './dashboard.html',
  styleUrl: '../../attendance/attendance.css',
})
export class Dashboard {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/company/dashboard`;
  private auth = inject(AuthService);
  private notify = inject(NotifyService);
  readonly brand = inject(BrandService);
  readonly dt12 = dt12;
  readonly ranges = [7, 14, 30];

  days = signal(14);
  me = signal<MeData | null>(null);
  team = signal<TeamData | null>(null);
  stamp = signal<Date | null>(null);
  loading = signal(true);

  stampText = computed(
    () => this.stamp()?.toLocaleTimeString('en-IN', { timeZone: 'Asia/Kolkata' }) ?? '',
  );

  greeting = computed(() => {
    const h =
      Number(
        new Date().toLocaleString('en-IN', {
          hour: 'numeric',
          hour12: false,
          timeZone: 'Asia/Kolkata',
        }),
      ) % 24;
    const part = h < 12 ? 'Good morning' : h < 17 ? 'Good afternoon' : 'Good evening';
    return `${part}, ${this.auth.user()?.name ?? ''}`;
  });

  hasTeam = computed(() => !!this.team() && this.team()!.available !== false);
  private text = computed(() => this.brand.navbarText());

  // ---------- KPI cards ----------
  teamKpis = computed<Kpi[]>(() => {
    const t = this.team();
    if (!t || t.available === false) return [];
    const d = t.trend[t.trend.length - 1];
    const pct = t.people ? Math.round((d.present / t.people) * 100) : 0;
    const open =
      (t.tasks.byStatus['todo'] ?? 0) +
      (t.tasks.byStatus['in_progress'] ?? 0) +
      (t.tasks.byStatus['on_hold'] ?? 0);
    return [
      {
        label: 'People',
        value: t.people,
        sub: 'in your view',
        cls: 'from-indigo-500 to-violet-600',
      },
      {
        label: 'Present today',
        value: d.present,
        sub: `${pct}% of people`,
        cls: 'from-emerald-500 to-teal-600',
      },
      {
        label: 'Absent today',
        value: d.absent,
        sub: 'marked absent',
        cls: 'from-rose-500 to-red-600',
      },
      { label: 'On leave', value: d.leave, sub: 'approved leave', cls: 'from-cyan-500 to-sky-600' },
      {
        label: 'Late today',
        value: d.late,
        sub: 'came after start',
        cls: 'from-amber-500 to-orange-600',
      },
      {
        label: 'To approve',
        value: t.requests.attendance + t.requests.leave + t.requests.taskApprovals,
        sub: `${t.requests.attendance} attendance · ${t.requests.leave} leave · ${t.requests.taskApprovals} tasks`,
        cls: 'from-fuchsia-500 to-pink-600',
      },
      {
        label: 'Open tasks',
        value: open,
        sub: `${t.tasks.avgProgress}% average progress`,
        cls: 'from-blue-500 to-indigo-600',
      },
      {
        label: 'Overdue tasks',
        value: t.tasks.overdue,
        sub: `${t.tasks.waiting} waiting approval`,
        cls: 'from-red-500 to-orange-600',
      },
    ];
  });

  meKpis = computed<Kpi[]>(() => {
    const m = this.me();
    if (!m) return [];
    return [
      {
        label: 'Attendance rate',
        value: m.rate == null ? '—' : m.rate + '%',
        sub: `last ${m.days} days`,
        cls: 'from-emerald-500 to-teal-600',
      },
      {
        label: 'Present days',
        value: m.present,
        sub: `${m.late} late`,
        cls: 'from-sky-500 to-blue-600',
      },
      {
        label: 'Absent days',
        value: m.absent,
        sub: `${m.leave} on leave`,
        cls: 'from-rose-500 to-red-600',
      },
      {
        label: 'Present streak',
        value: m.streak,
        sub: 'days in a row',
        cls: 'from-amber-500 to-orange-600',
      },
      {
        label: 'Open tasks',
        value: m.tasks.open,
        sub: `${m.tasks.overdue} overdue`,
        cls: 'from-indigo-500 to-violet-600',
      },
      {
        label: 'Waiting',
        value: m.tasks.waiting + m.pending.attendance + m.pending.leave,
        sub: `${m.pending.attendance} attendance · ${m.pending.leave} leave · ${m.tasks.waiting} tasks`,
        cls: 'from-fuchsia-500 to-pink-600',
      },
    ];
  });

  // ---------- team charts ----------
  tTrend = computed(() => {
    const t = this.team();
    const d = t?.trend ?? [];
    return stack(
      d.map((x) => shortDate(x.date)),
      [
        { label: 'Present', data: d.map((x) => x.present), color: C.green },
        { label: 'Absent', data: d.map((x) => x.absent), color: C.red },
        { label: 'Leave', data: d.map((x) => x.leave), color: C.teal },
        { label: 'Holiday', data: d.map((x) => x.holiday), color: C.sky },
      ],
      this.text(),
    );
  });

  tToday = computed(() => {
    const t = this.team();
    const d = t?.trend[t.trend.length - 1];
    const rest = Math.max(
      0,
      (t?.people ?? 0) - (d ? d.present + d.absent + d.leave + d.holiday : 0),
    );
    return donut(
      ['Present', 'Absent', 'Leave', 'Holiday', 'Not marked yet'],
      [d?.present ?? 0, d?.absent ?? 0, d?.leave ?? 0, d?.holiday ?? 0, rest],
      [C.green, C.red, C.teal, C.sky, C.slate],
      this.text(),
    );
  });

  tLate = computed(() => bars(BAND_LABELS, this.team()?.lateBands ?? [], BAND_COLORS, this.text()));

  tRole = computed(() => {
    const r = this.team()?.byRole ?? [];
    return stack(
      r.map((x) => x.role),
      [
        { label: 'Present', data: r.map((x) => x.present), color: C.green },
        { label: 'Absent', data: r.map((x) => x.absent), color: C.red },
        { label: 'Leave', data: r.map((x) => x.leave), color: C.teal },
      ],
      this.text(),
      true,
    );
  });

  tTasks = computed(() => this.tasksDonut(this.team()?.tasks.byStatus ?? {}));
  tTaskTrend = computed(() => this.taskLines(this.team()?.tasks.trend ?? []));

  tLeave7 = computed(() => {
    const d = this.team()?.leave7 ?? [];
    return bars(
      d.map((x) => shortDate(x.date)),
      d.map((x) => x.n),
      d.map(() => C.teal),
      this.text(),
    );
  });

  // ---------- my charts ----------
  mTrend = computed(() => {
    const s = this.me()?.series ?? [];
    const of = (st: string) => s.map((x) => (x.status === st ? 1 : 0));
    return stack(
      s.map((x) => shortDate(x.date)),
      [
        { label: 'Present', data: of('present'), color: C.green },
        { label: 'Absent', data: of('absent'), color: C.red },
        { label: 'Leave', data: of('leave'), color: C.teal },
        { label: 'Holiday', data: of('holiday'), color: C.sky },
      ],
      this.text(),
    );
  });

  mLate = computed(() => {
    const s = this.me()?.series ?? [];
    return lines(
      s.map((x) => shortDate(x.date)),
      [{ label: 'Minutes late', data: s.map((x) => x.late), color: C.amber }],
      this.text(),
    );
  });

  mTasks = computed(() => this.tasksDonut(this.me()?.tasks.byStatus ?? {}));
  mTaskTrend = computed(() => this.taskLines(this.me()?.tasks.trend ?? []));

  private tasksDonut(b: Record<string, number>) {
    return donut(
      ['To do', 'In progress', 'On hold', 'Done'],
      [b['todo'] ?? 0, b['in_progress'] ?? 0, b['on_hold'] ?? 0, b['done'] ?? 0],
      [C.slate, C.sky, C.amber, C.green],
      this.text(),
    );
  }

  private taskLines(t: TaskTrend[]) {
    return lines(
      t.map((x) => shortDate(x.date)),
      [
        { label: 'Created', data: t.map((x) => x.created), color: C.indigo },
        { label: 'Completed', data: t.map((x) => x.done), color: C.green },
      ],
      this.text(),
    );
  }

  constructor() {
    this.load();

    const id = setInterval(() => {
      if (!document.hidden) this.load();
    }, 30000);
    inject(DestroyRef).onDestroy(() => clearInterval(id));

    // a new alert (approval, task, leave...) refreshes the numbers at once
    let first = true;
    effect(() => {
      this.notify.pulse();
      untracked(() => {
        if (first) {
          first = false;
          return;
        }
        this.load();
      });
    });
  }

  setDays(n: number) {
    this.days.set(n);
    this.load();
  }

  load() {
    const params = { days: this.days() };
    this.http.get<MeData>(`${this.api}/me`, { params }).subscribe({
      next: (d) => {
        this.me.set(d);
        this.stamp.set(new Date());
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
    this.http.get<TeamData>(`${this.api}/team`, { params }).subscribe({
      next: (d) => this.team.set(d),
      error: () => this.team.set(null),
    });
  }
}
