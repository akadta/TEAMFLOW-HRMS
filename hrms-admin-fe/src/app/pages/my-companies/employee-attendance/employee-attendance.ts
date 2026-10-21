import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { DecimalPipe } from '@angular/common';
import {
  BANDS,
  DAYS,
  DayRow,
  ReqItem,
  STATUS,
  band,
  dayLabel,
  dow,
  dt12,
  errMsg,
  fileUrl,
  istDate,
  lateText,
  mixWhite,
  rgb,
  shift,
  srcLabel,
  t12,
  textOn,
  toHex,
  hhmm,
  mapUrl,
  result,
  RESULT_CLS,
} from '../mc.util';

const PAGE = 50;
type Kind = 'excel' | 'pdf';

interface EmpDetail {
  id: number;
  userId: number;
  employeeCode: string;
  name: string;
  email: string;
  phone: string | null;
  role: string | null;
  workType: string | null;
  profilePicture: string | null;
  isActive: boolean;
}

interface SlotItem {
  date: string;
  from: string;
  to: string;
  status: string;
  at: string | null;
  lat: number | null;
  lng: number | null;
}
interface LoginItem {
  at: string;
  lat: number;
  lng: number;
  accuracy: number | null;
}

@Component({
  selector: 'app-employee-attendance',
  imports: [DecimalPipe],
  templateUrl: './employee-attendance.html',
})
export class EmployeeAttendance {
  private http = inject(HttpClient);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private cid = Number(this.route.snapshot.paramMap.get('companyId'));
  private eid = Number(this.route.snapshot.paramMap.get('employeeId'));
  private api = `${environment.apiUrl}/main/my-companies/${this.cid}/employees/${this.eid}/attendance`;
  private today = istDate(Date.now());
  private base = `${environment.apiUrl}/main/my-companies/${this.cid}/employees/${this.eid}`;
  readonly STATUS = STATUS;
  readonly BANDS = BANDS;
  readonly DAYS = DAYS;
  readonly band = band;
  readonly lateText = lateText;
  readonly dayLabel = dayLabel;
  readonly t12 = t12;
  readonly dt12 = dt12;
  readonly fileUrl = fileUrl;
  readonly srcLabel = srcLabel;
  readonly hhmm = hhmm;
  readonly mapUrl = mapUrl;
  readonly result = result;
  readonly RES = RESULT_CLS;
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
  readonly textOn = textOn;
  readonly chips = ['all', 'present', 'absent', 'pending', 'holiday', 'leave'];
  readonly reqChips = ['all', 'pending', 'approved', 'rejected'];
  readonly presets = [
    { id: 'month', label: 'This month' },
    { id: 'last', label: 'Last month' },
    { id: '7', label: 'Last 7 days' },
    { id: '30', label: 'Last 30 days' },
    { id: 'year', label: 'This year' },
  ];
  readonly themes = [
    { id: 'blue', label: 'Blue', hex: '#2563eb' },
    { id: 'green', label: 'Green', hex: '#059669' },
    { id: 'red', label: 'Red', hex: '#dc2626' },
    { id: 'purple', label: 'Purple', hex: '#7c3aed' },
    { id: 'orange', label: 'Orange', hex: '#ea580c' },
    { id: 'slate', label: 'Slate', hex: '#334155' },
  ];
  readonly inp =
    'rounded-lg border border-slate-200 bg-white/80 px-2.5 py-1.5 text-xs text-slate-900 outline-none transition focus:border-blue-400 focus:ring-2 focus:ring-blue-200';

  emp = signal<EmpDetail | null>(null);
  company = signal('');
  rows = signal<DayRow[]>([]);
  reqs = signal<ReqItem[]>([]);
  slots = signal<SlotItem[]>([]);
  logins = signal<LoginItem[]>([]);
  slotStatus = signal('all');

  slotCounts = computed(() => {
    const c: Record<string, number> = { all: this.slots().length };
    for (const s of this.slots()) c[s.status] = (c[s.status] ?? 0) + 1;
    return c;
  });

  slotShown = computed(() =>
    this.slots().filter((s) => this.slotStatus() === 'all' || s.status === this.slotStatus()),
  );
  loading = signal(true);
  error = signal<string | null>(null);

  tab = signal<'daily' | 'requests' | 'checkins' | 'logins'>('daily');
  from = signal(this.today.slice(0, 8) + '01');
  to = signal(this.today);
  status = signal('all');
  bandIdx = signal(-1);
  source = signal('all');
  dayIdx = signal(-1);
  q = signal('');
  asc = signal(false);
  page = signal(0);
  reqStatus = signal('all');

  // export modal
  kind = signal<Kind | null>(null);
  busy = signal<Kind | null>(null);
  themeId = signal('blue');
  withReq = signal(true);

  pickedHex = computed(() => this.themes.find((t) => t.id === this.themeId())?.hex ?? '#2563eb');

  counts = computed(() => {
    const c: Record<string, number> = { all: this.rows().length };
    for (const r of this.rows()) c[r.status] = (c[r.status] ?? 0) + 1;
    return c;
  });

  filtered = computed(() => {
    const st = this.status();
    const b = this.bandIdx();
    const src = this.source();
    const d = this.dayIdx();
    const q = this.q().trim().toLowerCase();

    const list = this.rows().filter(
      (r) =>
        (st === 'all' || r.status === st) &&
        (src === 'all' || r.source === src) &&
        (d < 0 || dow(r.date) === d) &&
        (b < 0 || (!!r.checkInAt && BANDS.indexOf(band(r.lateMinutes)) === b)) &&
        (!q || (r.note ?? '').toLowerCase().includes(q)),
    );
    return this.asc() ? [...list].reverse() : list; // server sends newest first
  });

  pages = computed(() => Math.max(1, Math.ceil(this.filtered().length / PAGE)));
  cur = computed(() => Math.min(this.page(), this.pages() - 1));
  pageRows = computed(() => this.filtered().slice(this.cur() * PAGE, this.cur() * PAGE + PAGE));

  reqCounts = computed(() => {
    const c: Record<string, number> = { all: this.reqs().length };
    for (const r of this.reqs()) c[r.status] = (c[r.status] ?? 0) + 1;
    return c;
  });

  reqShown = computed(() =>
    this.reqs().filter((r) => this.reqStatus() === 'all' || r.status === this.reqStatus()),
  );

  constructor() {
    this.load();
  }

  val(e: Event) {
    return (e.target as HTMLInputElement).value;
  }

  num(e: Event) {
    return Number((e.target as HTMLSelectElement).value);
  }

  back() {
    this.router.navigate(['/my-attendance', this.cid]);
  }

  private loadExtra() {
    const params = { from: this.from(), to: this.to() };
    this.http.get<{ rows: SlotItem[] }>(`${this.base}/checkins`, { params }).subscribe({
      next: (r) => this.slots.set(r.rows),
      error: () => this.slots.set([]),
    });
    this.http.get<{ rows: LoginItem[] }>(`${this.base}/logins`, { params }).subscribe({
      next: (r) => this.logins.set(r.rows),
      error: () => this.logins.set([]),
    });
  }

  load() {
    this.loadExtra();
    this.loading.set(true);
    this.error.set(null);
    this.http
      .get<{ company: { name: string }; employee: EmpDetail; rows: DayRow[]; requests: ReqItem[] }>(
        this.api,
        { params: { from: this.from(), to: this.to() } },
      )
      .subscribe({
        next: (r) => {
          this.emp.set(r.employee);
          this.company.set(r.company.name);
          this.rows.set(r.rows);
          this.reqs.set(r.requests);
          this.loading.set(false);
        },
        error: (e: HttpErrorResponse) => {
          this.loading.set(false);
          this.error.set(errMsg(e, 'Could not load the report.'));
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
      this.from.set(shift(t, -6));
      this.to.set(t);
    } else if (k === '30') {
      this.from.set(shift(t, -29));
      this.to.set(t);
    } else {
      this.from.set(`${y}-01-01`);
      this.to.set(t);
    }

    this.load();
  }

  reset() {
    this.status.set('all');
    this.bandIdx.set(-1);
    this.source.set('all');
    this.dayIdx.set(-1);
    this.q.set('');
    this.asc.set(false);
    this.page.set(0);
  }

  // ---------- export ----------
  ask(k: Kind) {
    if (this.busy()) return;
    this.kind.set(k);
  }

  async confirm() {
    const k = this.kind();
    if (!k || this.busy()) return;

    this.busy.set(k);
    await new Promise((r) => setTimeout(r, 60)); // let the spinner paint
    try {
      if (k === 'excel') await this.excel(this.pickedHex());
      else await this.pdf(this.pickedHex());
    } catch {
      this.error.set('Could not create the file. Try again.');
    } finally {
      this.busy.set(null);
      this.kind.set(null);
    }
  }

  private head = [
    'Date',
    'Day',
    'Attendance',
    'Status',
    'Check-in (IST)',
    'Late (min)',
    'Source',
    'Note',
  ];
  private reqHead = [
    'Date',
    'Sent at (IST)',
    'Late (min)',
    'Status',
    'Reason',
    'Decided by',
    'Decided at (IST)',
    'Decision note',
    'Proof files',
  ];

  private body() {
    return this.filtered().map((r) => [
      r.date,
      DAYS[dow(r.date)],
      result(r.status),
      STATUS[r.status].label,
      r.checkInAt ? t12(r.checkInAt) : '',
      r.checkInAt ? r.lateMinutes : '',
      srcLabel(r.source),
      r.note ?? '',
    ]);
  }

  private reqBody() {
    return this.reqShown().map((r) => [
      r.date,
      dt12(r.requestedAt),
      r.lateMinutes,
      STATUS[r.status].label,
      r.reason,
      r.decidedBy ?? '',
      r.decidedAt ? dt12(r.decidedAt) : '',
      r.decisionNote ?? '',
      r.files.map((f) => f.fileName).join(', '),
    ]);
  }

  private summary() {
    const c = this.counts();
    return `Present ${c['present'] ?? 0}  |  Late ${c['late'] ?? 0}  |  Absent ${c['absent'] ?? 0}  |  Holiday ${c['holiday'] ?? 0}  |  Pending ${c['pending'] ?? 0}`;
  }

  // header + zebra rows + borders, status text coloured
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private paint(
    XLSX: any,
    ws: any,
    headRow: number,
    cols: number,
    n: number,
    hex: string,
    statusCol: number,
    statusHex: string[],
  ) {
    const headBg = toHex(rgb(hex));
    const headFg = textOn(hex) === '#ffffff' ? 'FFFFFF' : '0F172A';
    const zebra = toHex(mixWhite(hex, 0.88));
    const edge = { style: 'thin', color: { rgb: 'D1D5DB' } };
    const border = { top: edge, bottom: edge, left: edge, right: edge };

    for (let r = 0; r <= n; r++) {
      for (let c = 0; c < cols; c++) {
        const cell = ws[XLSX.utils.encode_cell({ r: headRow + r, c })];
        if (!cell) continue;
        cell.s =
          r === 0
            ? {
                font: { bold: true, color: { rgb: headFg } },
                fill: { patternType: 'solid', fgColor: { rgb: headBg } },
                alignment: { vertical: 'center' },
                border,
              }
            : {
                fill: { patternType: 'solid', fgColor: { rgb: r % 2 === 0 ? zebra : 'FFFFFF' } },
                alignment: { vertical: 'top', wrapText: true },
                border,
                ...(c === statusCol && statusHex[r - 1]
                  ? { font: { bold: true, color: { rgb: statusHex[r - 1] } } }
                  : {}),
              };
      }
    }
  }

  private async excel(hex: string) {
    // xlsx-js-style = xlsx with cell colours
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const mod: any = await import('xlsx-js-style');
    const XLSX = mod.default ?? mod;
    const e = this.emp()!;
    const list = this.filtered();

    const top = [
      ['Attendance report'],
      [`Employee: ${e.name} (${e.employeeCode})    Company: ${this.company()}`],
      [`Period: ${this.from()} to ${this.to()}    Rows: ${list.length}`],
      [this.summary()],
      [],
    ];

    const ws = XLSX.utils.aoa_to_sheet([...top, this.head, ...this.body()]);
    ws['A1'].s = { font: { bold: true, sz: 16, color: { rgb: toHex(rgb(hex)) } } };
    ws['!merges'] = [0, 1, 2, 3].map((r) => ({
      s: { r, c: 0 },
      e: { r, c: this.head.length - 1 },
    }));
    ws['!cols'] = [12, 7, 12, 12, 16, 10, 10, 34].map((wch) => ({ wch }));
    this.paint(
      XLSX,
      ws,
      top.length,
      this.head.length,
      list.length,
      hex,
      3,
      list.map((r) => STATUS[r.status].hex.slice(1).toUpperCase()),
    );
    ws['!autofilter'] = { ref: `A${top.length + 1}:H${top.length + 1 + list.length}` };

    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Attendance');

    const rq = this.reqShown();
    if (this.withReq() && rq.length) {
      const ws2 = XLSX.utils.aoa_to_sheet([
        ['Attendance requests'],
        [`Employee: ${e.name} (${e.employeeCode})    Requests: ${rq.length}`],
        [],
        this.reqHead,
        ...this.reqBody(),
      ]);
      ws2['A1'].s = { font: { bold: true, sz: 16, color: { rgb: toHex(rgb(hex)) } } };
      ws2['!cols'] = [12, 18, 10, 11, 48, 16, 18, 28, 32].map((wch) => ({ wch }));
      this.paint(
        XLSX,
        ws2,
        3,
        this.reqHead.length,
        rq.length,
        hex,
        3,
        rq.map((r) => STATUS[r.status].hex.slice(1).toUpperCase()),
      );
      XLSX.utils.book_append_sheet(wb, ws2, 'Requests');
    }

    XLSX.writeFile(wb, `attendance_${e.employeeCode}_${this.from()}_${this.to()}.xlsx`);
  }

  private async pdf(hex: string) {
    const { jsPDF } = await import('jspdf');
    const autoTable = (await import('jspdf-autotable')).default;
    const e = this.emp()!;
    const list = this.filtered();
    const accent = rgb(hex);
    const headText = rgb(textOn(hex));

    const doc = new jsPDF({ orientation: 'landscape' });
    doc.setFontSize(15);
    doc.setTextColor(accent[0], accent[1], accent[2]);
    doc.text('Attendance report', 14, 13);
    doc.setFontSize(9);
    doc.setTextColor(30, 41, 59);
    doc.text(`Employee: ${e.name} (${e.employeeCode})   |   Company: ${this.company()}`, 14, 19);
    doc.text(`Period: ${this.from()} to ${this.to()}   |   Rows: ${list.length}`, 14, 24);
    doc.text(this.summary(), 14, 29);

    autoTable(doc, {
      startY: 33,
      head: [this.head],
      body: this.body(),
      styles: { fontSize: 8, cellPadding: 1.5 },
      headStyles: { fillColor: accent, textColor: headText },
      alternateRowStyles: { fillColor: mixWhite(hex, 0.9) },
      // eslint-disable-ne  xt-line @typescript-eslint/no-explicit-any
      didParseCell: (d: any) => {
        if (d.section !== 'body') return;
        const r = list[d.row.index];
        if (d.column.index === 2)
          d.cell.styles.textColor =
            result(r.status) === 'Present'
              ? '#059669'
              : result(r.status) === 'Absent'
                ? '#dc2626'
                : '#94a3b8';
        if (d.column.index === 3) d.cell.styles.textColor = STATUS[r.status].hex;
        if (d.column.index === 5 && r.checkInAt) d.cell.styles.textColor = band(r.lateMinutes).hex;
      },
    });

    const rq = this.reqShown();
    if (this.withReq() && rq.length) {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const y = ((doc as any).lastAutoTable?.finalY ?? 33) + 10;
      doc.setFontSize(12);
      doc.setTextColor(accent[0], accent[1], accent[2]);
      doc.text('Attendance requests', 14, y);

      autoTable(doc, {
        startY: y + 3,
        head: [this.reqHead],
        body: this.reqBody(),
        styles: { fontSize: 7.5, cellPadding: 1.5 },
        headStyles: { fillColor: accent, textColor: headText },
        alternateRowStyles: { fillColor: mixWhite(hex, 0.9) },
        columnStyles: { 4: { cellWidth: 70 } },
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        didParseCell: (d: any) => {
          if (d.section === 'body' && d.column.index === 3)
            d.cell.styles.textColor = STATUS[rq[d.row.index].status].hex;
        },
      });
    }

    doc.save(`attendance_${e.employeeCode}_${this.from()}_${this.to()}.pdf`);
  }
}
