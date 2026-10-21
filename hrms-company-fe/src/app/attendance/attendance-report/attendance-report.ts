import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { BrandService } from '../../core/brand/brand.service';
import { AuthService } from '../../core/auth/auth.service';
import {
  BANDS,
  ReportRow,
  STATUS,
  band,
  dayLabel,
  errMsg,
  istDate,
  lateText,
  t12,
  result,
} from '../attendance.util';

const PAGE = 50;

type Kind = 'excel' | 'pdf';

// '#abc' / '#aabbcc' -> '#aabbcc' (falls back to blue)
function norm(hex: string | null | undefined): string {
  const h = (hex ?? '').replace('#', '').trim();
  const full = h.length === 3 ? h.replace(/(.)/g, '$1$1') : h;
  return /^[0-9a-fA-F]{6}$/.test(full) ? '#' + full.toLowerCase() : '#2563eb';
}

function rgb(hex: string): [number, number, number] {
  const n = parseInt(hex.slice(1), 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}

// mix with white: amt 0 = same colour, 1 = white
function mixWhite(hex: string, amt: number): [number, number, number] {
  return rgb(hex).map((c) => Math.round(c + (255 - c) * amt)) as [number, number, number];
}

const toHex = (c: [number, number, number]) =>
  c
    .map((x) => x.toString(16).padStart(2, '0'))
    .join('')
    .toUpperCase();

@Component({
  selector: 'app-attendance-report',
  templateUrl: './attendance-report.html',
  styleUrl: '../attendance.css',
})
export class AttendanceReport {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/company/attendance/report`;
  readonly brand = inject(BrandService);

  readonly STATUS = STATUS;
  readonly BANDS = BANDS;
  readonly band = band;
  readonly lateText = lateText;
  readonly dayLabel = dayLabel;
  readonly result = result;
  readonly t12 = t12;
  readonly chips = ['all', 'present', 'absent', 'pending', 'holiday', 'leave'];
  private today = istDate(Date.now());

  from = signal(this.today.slice(0, 8) + '01');
  to = signal(this.today);
  rows = signal<ReportRow[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  search = signal('');
  role = signal('');
  status = signal('all');
  bandIdx = signal(-1);
  source = signal('all');
  page = signal(0);
  employee = signal('');

  employees = computed(() => {
    const m = new Map<string, string>();
    for (const r of this.rows()) m.set(String(r.userId), `${r.name} (${r.code})`);
    return [...m]
      .map(([id, label]) => ({ id, label }))
      .sort((a, b) => a.label.localeCompare(b.label));
  });

  pickedEmployee = computed(
    () => this.employees().find((x) => x.id === this.employee())?.label ?? null,
  );

  // rows of the selected employee (or everyone)
  private byEmployee = computed(() => {
    const e = this.employee();
    return e ? this.rows().filter((r) => String(r.userId) === e) : this.rows();
  });

  // used in file names, e.g. "EMP001_"
  private fileTag = computed(() => {
    const r = this.byEmployee()[0];
    return this.employee() && r ? `${r.code}_` : '';
  });

  // ---------- export state ----------
  kind = signal<Kind | null>(null); // which export modal is open
  busy = signal<Kind | null>(null); // which export is being built (spinner)
  themeId = signal('company');
  // ---------- override (owner / approval permission) ----------
  private auth = inject(AuthService);
  canOverride = computed(() => {
    const u = this.auth.user();
    return !!u && (u.type === 'owner' || u.permissions.includes('attendance.approval'));
  });
  ov = signal<ReportRow | null>(null);
  ovStatus = signal<'present' | 'absent'>('present');
  ovNote = signal('');
  ovFiles = signal<File[]>([]);
  ovBusy = signal(false);
  ovError = signal<string | null>(null);

  canFix(r: ReportRow) {
    return this.canOverride() && ['absent', 'pending', 'present', 'late'].includes(r.status);
  }

  openOv(r: ReportRow) {
    this.ovStatus.set(r.status === 'present' || r.status === 'late' ? 'absent' : 'present');
    this.ovNote.set('');
    this.ovFiles.set([]);
    this.ovError.set(null);
    this.ov.set(r);
  }

  pickOv(e: Event) {
    const el = e.target as HTMLInputElement;
    const add = Array.from(el.files ?? []);
    el.value = '';
    const all = [...this.ovFiles(), ...add];
    if (all.length > 5) return this.ovError.set('Max 5 files.');
    if (add.some((f) => f.size > 8 * 1024 * 1024)) return this.ovError.set('Each file: max 8 MB.');
    this.ovError.set(null);
    this.ovFiles.set(all);
  }

  dropOv(i: number) {
    this.ovFiles.update((l) => l.filter((_, x) => x !== i));
  }

  saveOv() {
    const r = this.ov();
    if (!r || this.ovBusy()) return;

    const note = this.ovNote().trim();
    if (note.length < 5) return this.ovError.set('Write a note (min 5 characters).');
    if (this.ovStatus() === 'present' && this.ovFiles().length === 0)
      return this.ovError.set('Add at least one proof document.');

    const fd = new FormData();
    fd.append('userId', String(r.userId));
    fd.append('date', r.date);
    fd.append('status', this.ovStatus());
    fd.append('note', note);
    this.ovFiles().forEach((f) => fd.append('files', f));

    this.ovBusy.set(true);
    this.ovError.set(null);
    this.http.post(`${environment.apiUrl}/company/attendance/requests/override`, fd).subscribe({
      next: () => {
        this.ovBusy.set(false);
        this.ov.set(null);
        this.load();
      },
      error: (e: HttpErrorResponse) => {
        this.ovBusy.set(false);
        this.ovError.set(errMsg(e, 'Could not save override.'));
      },
    });
  }

  private readonly presets = [
    { id: 'blue', label: 'Blue', hex: '#2563eb' },
    { id: 'green', label: 'Green', hex: '#059669' },
    { id: 'red', label: 'Red', hex: '#dc2626' },
    { id: 'purple', label: 'Purple', hex: '#7c3aed' },
    { id: 'orange', label: 'Orange', hex: '#ea580c' },
    { id: 'slate', label: 'Slate', hex: '#334155' },
  ];

  options = computed(() => [
    { id: 'company', label: 'Company', hex: norm(this.brand.theme().accent) },
    ...this.presets,
  ]);

  pickedHex = computed(
    () =>
      this.options().find((o) => o.id === this.themeId())?.hex ?? norm(this.brand.theme().accent),
  );

  // header text colour that stays readable on the chosen colour
  textOn(hex: string): string {
    const [r, g, b] = rgb(hex);
    return 0.299 * r + 0.587 * g + 0.114 * b > 150 ? '#0f172a' : '#ffffff';
  }

  // light version of the colour for zebra rows
  tint(hex: string): string {
    return '#' + toHex(mixWhite(hex, 0.88));
  }

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
    const rows = this.byEmployee();
    const c: Record<string, number> = { all: rows.length };
    for (const r of rows) c[r.status] = (c[r.status] ?? 0) + 1;
    return c;
  });

  filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    const st = this.status();
    const role = this.role();
    const b = this.bandIdx();
    const src = this.source();

    return this.byEmployee().filter(
      (r) =>
        (st === 'all' || r.status === st) &&
        (!role || r.role === role) &&
        (src === 'all' || r.source === src) &&
        (b < 0 || (!!r.checkInAt && BANDS.indexOf(band(r.lateMinutes)) === b)) &&
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
      .get<{ rows: ReportRow[] }>(this.api, { params: { from: this.from(), to: this.to() } })
      .subscribe({
        next: (r) => {
          this.rows.set(r.rows);
          if (!r.rows.some((x) => String(x.userId) === this.employee())) this.employee.set('');
          this.loading.set(false);
        },
        error: (e: HttpErrorResponse) => {
          this.loading.set(false);
          this.rows.set([]);
          this.error.set(errMsg(e, 'Could not load report.'));
        },
      });
  }

  reset() {
    this.search.set('');
    this.role.set('');
    this.status.set('all');
    this.bandIdx.set(-1);
    this.source.set('all');
    this.page.set(0);
    this.employee.set('');
  }

  // ---------- export ----------
  ask(k: Kind) {
    if (this.busy()) return;
    this.error.set(null);
    this.kind.set(k);
  }

  async confirm() {
    const k = this.kind();
    if (!k || this.busy()) return;

    this.busy.set(k);
    // let the spinner paint before the heavy work starts
    await new Promise((r) => setTimeout(r, 60));
    try {
      const hex = this.pickedHex();
      if (k === 'excel') await this.excel(hex);
      else await this.pdf(hex);
    } catch {
      this.error.set('Could not create the file. Try again.');
    } finally {
      this.busy.set(null);
      this.kind.set(null);
    }
  }

  private head = [
    'Date',
    'Code',
    'Name',
    'Role',
    'Attendance',
    'Status',
    'Check-in (IST)',
    'Late (min)',
    'Source',
    'Note',
  ];

  private body() {
    return this.filtered().map((r) => [
      r.date,
      r.code,
      r.name,
      r.role ?? '',
      result(r.status),
      STATUS[r.status].label,
      r.checkInAt ? t12(r.checkInAt) : '',
      r.checkInAt ? r.lateMinutes : '',
      r.source ?? '',
      r.note ?? '',
    ]);
  }

  private async excel(hex: string) {
    // xlsx-js-style = xlsx with cell colours (npm i xlsx-js-style)
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const mod: any = await import('xlsx-js-style');
    const XLSX = mod.default ?? mod;

    const body = this.body();
    const ws = XLSX.utils.aoa_to_sheet([this.head, ...body]);

    const headBg = toHex(rgb(hex));
    const headFg = this.textOn(hex) === '#ffffff' ? 'FFFFFF' : '0F172A';
    const zebra = toHex(mixWhite(hex, 0.88));
    const edge = { style: 'thin', color: { rgb: 'D1D5DB' } };
    const border = { top: edge, bottom: edge, left: edge, right: edge };

    for (let r = 0; r <= body.length; r++) {
      for (let c = 0; c < this.head.length; c++) {
        const cell = ws[XLSX.utils.encode_cell({ r, c })];
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
                border,
              };
      }
    }

    ws['!cols'] = [12, 12, 24, 16, 12, 12, 16, 10, 10, 24].map((wch) => ({ wch }));
    ws['!autofilter'] = { ref: ws['!ref'] };

    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Attendance');
    XLSX.writeFile(wb, `attendance_${this.fileTag()}${this.from()}_${this.to()}.xlsx`);
  }

  private async pdf(hex: string) {
    const { jsPDF } = await import('jspdf');
    const autoTable = (await import('jspdf-autotable')).default;
    const list = this.filtered();
    const accent = rgb(hex);
    const headText = rgb(this.textOn(hex));

    const doc = new jsPDF({ orientation: 'landscape' });
    doc.setFontSize(12);
    doc.setTextColor(accent[0], accent[1], accent[2]);
    const who = this.pickedEmployee();
    doc.text(
      `Attendance report${who ? ' - ' + who : ''}  ${this.from()} to ${this.to()}  (${list.length} rows)`,
      14,
      12,
    );
    doc.setTextColor(0, 0, 0);

    autoTable(doc, {
      startY: 16,
      head: [this.head],
      body: this.body(),
      styles: { fontSize: 8, cellPadding: 1.5 },
      headStyles: { fillColor: accent, textColor: headText },
      alternateRowStyles: { fillColor: mixWhite(hex, 0.9) },
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      didParseCell: (d: any) => {
        if (d.section !== 'body') return;
        const r = list[d.row.index];
        if (d.column.index === 4)
          d.cell.styles.textColor =
            result(r.status) === 'Present'
              ? '#059669'
              : result(r.status) === 'Absent'
                ? '#dc2626'
                : '#94a3b8';
        if (d.column.index === 5) d.cell.styles.textColor = STATUS[r.status].hex;
        if (d.column.index === 7 && r.checkInAt) d.cell.styles.textColor = band(r.lateMinutes).hex;
      },
    });

    doc.save(`attendance_${this.fileTag()}${this.from()}_${this.to()}.pdf`);
  }
}
