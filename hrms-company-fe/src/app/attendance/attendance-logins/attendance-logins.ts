import { DecimalPipe } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { BrandService } from '../../core/brand/brand.service';
import { dt12, errMsg, istDate, mapUrl } from '../attendance.util';

interface Row {
  at: string;
  userId: number;
  code: string;
  name: string;
  role: string | null;
  lat: number;
  lng: number;
  accuracy: number | null;
}

const PAGE = 50;
const shiftIso = (iso: string, n: number) => {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, d + n)).toISOString().slice(0, 10);
};

@Component({
  selector: 'app-attendance-logins',
  imports: [DecimalPipe],
  templateUrl: './attendance-logins.html',
  styleUrl: '../attendance.css',
})
export class AttendanceLogins {
  private http = inject(HttpClient);
  private today = istDate(Date.now());
  readonly brand = inject(BrandService);

  readonly dt12 = dt12;
  readonly mapUrl = mapUrl;

  from = signal(shiftIso(this.today, -6));
  to = signal(this.today);
  rows = signal<Row[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  search = signal('');
  role = signal('');
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

  filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.rows().filter(
      (r) =>
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
      .get<{ rows: Row[] }>(`${environment.apiUrl}/company/attendance/logins`, {
        params: { from: this.from(), to: this.to() },
      })
      .subscribe({
        next: (r) => {
          this.rows.set(r.rows);
          this.loading.set(false);
        },
        error: (e: HttpErrorResponse) => {
          this.loading.set(false);
          this.rows.set([]);
          this.error.set(errMsg(e, 'Could not load login locations.'));
        },
      });
  }

  reset() {
    this.search.set('');
    this.role.set('');
    this.page.set(0);
  }
}
