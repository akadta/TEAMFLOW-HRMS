import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, computed, effect, inject, signal, untracked } from '@angular/core';
import { environment } from '../../../environments/environment';
import { ChartBox } from '../../core/chart/chart-box';
import { NotifyService } from '../../core/notify/notify.service';
import { dt12 } from '../my-companies/mc.util';
import { C, bars, donut, lines, shortDate, stack } from './dash.util';

interface Kpi {
  label: string;
  value: string | number;
  sub: string;
  cls: string;
}
interface TaskTrend {
  date: string;
  created: number;
  done: number;
}

interface MainData {
  companies: { total: number; active: number };
  people: number;
  trend: { date: string; present: number; absent: number; leave: number; holiday: number }[];
  byCompany: { name: string; people: number; present: number; absent: number; leave: number }[];
  leave: {
    pending: number;
    byStatus: Record<string, number>;
    next7: { date: string; n: number }[];
    latest: { id: number; name: string; company: string | null; days: number; createdAt: string }[];
  };
  tasks: {
    byStatus: Record<string, number>;
    overdue: number;
    waiting: number;
    awaitingOwner: number;
    trend: TaskTrend[];
    overdueList: {
      id: number;
      title: string;
      name: string;
      company: string | null;
      dueAt: string;
      progress: number;
    }[];
  };
}

const TEXT = '#475569';

@Component({
  selector: 'app-dashboard',
  imports: [ChartBox],
  templateUrl: './dashboard.html',
})
export class Dashboard {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/main/dashboard`;
  private notify = inject(NotifyService);
  readonly dt12 = dt12;
  readonly ranges = [7, 14, 30];

  days = signal(14);
  data = signal<MainData | null>(null);
  stamp = signal<Date | null>(null);
  error = signal<string | null>(null);

  stampText = computed(
    () => this.stamp()?.toLocaleTimeString('en-IN', { timeZone: 'Asia/Kolkata' }) ?? '',
  );

  kpis = computed<Kpi[]>(() => {
    const d = this.data();
    if (!d) return [];
    const t = d.trend[d.trend.length - 1];
    const pct = d.people ? Math.round((t.present / d.people) * 100) : 0;
    const open =
      (d.tasks.byStatus['todo'] ?? 0) +
      (d.tasks.byStatus['in_progress'] ?? 0) +
      (d.tasks.byStatus['on_hold'] ?? 0);
    return [
      {
        label: 'Companies',
        value: d.companies.total,
        sub: `${d.companies.active} active`,
        cls: 'from-indigo-500 to-violet-600',
      },
      { label: 'People', value: d.people, sub: 'in your view', cls: 'from-blue-500 to-indigo-600' },
      {
        label: 'Present today',
        value: t.present,
        sub: `${pct}% of people`,
        cls: 'from-emerald-500 to-teal-600',
      },
      {
        label: 'Absent today',
        value: t.absent,
        sub: 'marked absent',
        cls: 'from-rose-500 to-red-600',
      },
      { label: 'On leave', value: t.leave, sub: 'approved leave', cls: 'from-cyan-500 to-sky-600' },
      {
        label: 'Leave to approve',
        value: d.leave.pending,
        sub: 'waiting',
        cls: 'from-fuchsia-500 to-pink-600',
      },
      {
        label: 'Open tasks',
        value: open,
        sub: `${d.tasks.waiting} waiting approval`,
        cls: 'from-amber-500 to-orange-600',
      },
      {
        label: 'Overdue tasks',
        value: d.tasks.overdue,
        sub: `${d.tasks.awaitingOwner} awaiting company owner`,
        cls: 'from-red-500 to-orange-600',
      },
    ];
  });

  trend = computed(() => {
    const d = this.data()?.trend ?? [];
    return stack(
      d.map((x) => shortDate(x.date)),
      [
        { label: 'Present', data: d.map((x) => x.present), color: C.green },
        { label: 'Absent', data: d.map((x) => x.absent), color: C.red },
        { label: 'Leave', data: d.map((x) => x.leave), color: C.teal },
        { label: 'Holiday', data: d.map((x) => x.holiday), color: C.sky },
      ],
      TEXT,
    );
  });

  today = computed(() => {
    const d = this.data();
    const t = d?.trend[d.trend.length - 1];
    const rest = Math.max(
      0,
      (d?.people ?? 0) - (t ? t.present + t.absent + t.leave + t.holiday : 0),
    );
    return donut(
      ['Present', 'Absent', 'Leave', 'Holiday', 'Not marked yet'],
      [t?.present ?? 0, t?.absent ?? 0, t?.leave ?? 0, t?.holiday ?? 0, rest],
      [C.green, C.red, C.teal, C.sky, C.slate],
      TEXT,
    );
  });

  // attendance of the 10 biggest companies today, as % of their people
  companies = computed(() => {
    const c = this.data()?.byCompany ?? [];
    const pct = (n: number, p: number) => (p ? Math.round((n / p) * 100) : 0);
    return stack(
      c.map((x) => x.name),
      [
        { label: 'Present %', data: c.map((x) => pct(x.present, x.people)), color: C.green },
        { label: 'Absent %', data: c.map((x) => pct(x.absent, x.people)), color: C.red },
        { label: 'Leave %', data: c.map((x) => pct(x.leave, x.people)), color: C.teal },
      ],
      TEXT,
      true,
    );
  });

  leaveStatus = computed(() => {
    const b = this.data()?.leave.byStatus ?? {};
    return donut(
      ['Pending', 'Approved', 'Rejected', 'Cancelled'],
      [b['pending'] ?? 0, b['approved'] ?? 0, b['rejected'] ?? 0, b['cancelled'] ?? 0],
      [C.violet, C.green, C.red, C.slate],
      TEXT,
    );
  });

  leave7 = computed(() => {
    const d = this.data()?.leave.next7 ?? [];
    return bars(
      d.map((x) => shortDate(x.date)),
      d.map((x) => x.n),
      d.map(() => C.teal),
      TEXT,
    );
  });

  tasks = computed(() => {
    const b = this.data()?.tasks.byStatus ?? {};
    return donut(
      ['To do', 'In progress', 'On hold', 'Done'],
      [b['todo'] ?? 0, b['in_progress'] ?? 0, b['on_hold'] ?? 0, b['done'] ?? 0],
      [C.slate, C.sky, C.amber, C.green],
      TEXT,
    );
  });

  taskTrend = computed(() => {
    const t = this.data()?.tasks.trend ?? [];
    return lines(
      t.map((x) => shortDate(x.date)),
      [
        { label: 'Created', data: t.map((x) => x.created), color: C.indigo },
        { label: 'Completed', data: t.map((x) => x.done), color: C.green },
      ],
      TEXT,
    );
  });

  constructor() {
    this.load();

    const id = setInterval(() => {
      if (!document.hidden) this.load();
    }, 30000);
    inject(DestroyRef).onDestroy(() => clearInterval(id));

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
    this.http.get<MainData>(this.api, { params: { days: this.days() } }).subscribe({
      next: (d) => {
        this.data.set(d);
        this.stamp.set(new Date());
        this.error.set(null);
      },
      error: () => this.error.set('Could not load the dashboard.'),
    });
  }
}
