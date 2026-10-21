import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { BrandService } from '../../../core/brand/brand.service';
import { CanDirective } from '../../../core/rbac/can.directive';
import { PermissionService } from '../../../core/rbac/permission.service';

interface Row {
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

interface Detail {
  id: number;
  employeeCode: string;
  name: string;
  email: string;
  phone: string | null;
  emergencyNumber: string | null;
  officialEmail: string | null;
  dateOfBirth: string | null;
  gender: string | null;
  bloodGroup: string | null;
  maritalStatus: string | null;
  nationality: string | null;
  aadharNumber: string | null;
  panNumber: string | null;
  address: string | null;
  profilePicture: string | null;
  highestQualification: string | null;
  institution: string | null;
  yearOfPassing: number | null;
  role: string | null;
  workType: string | null;
  isActive: boolean;
  documents: {
    id: number;
    type: string;
    title: string;
    description: string | null;
    filePath: string | null;
  }[];
  histories: {
    id: number;
    employer: string;
    startDate: string | null;
    endDate: string | null;
    reasonForLeaving: string | null;
    relievingLetter: string | null;
  }[];
}

@Component({
  selector: 'app-employee-list',
  imports: [RouterLink, CanDirective],
  templateUrl: './employee-list.html',
  styleUrl: './employee-list.css',
})
export class EmployeeList {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/company/employee-list`;
  private host = environment.apiUrl.replace(/\/api\/?$/, '');
  private timer: ReturnType<typeof setTimeout> | undefined;
  readonly brand = inject(BrandService);
  readonly perm = inject(PermissionService);

  readonly pageSize = 50;
  readonly workLabel: Record<string, string> = {
    office: 'In Office',
    remote: 'Work From Home',
    hybrid: 'Hybrid',
  };

  rows = signal<Row[]>([]);
  total = signal(0);
  page = signal(1);
  search = signal('');
  status = signal('');

  loading = signal(true);
  busyId = signal<number | null>(null);
  error = signal<string | null>(null);

  detail = signal<Detail | null>(null);
  detailLoading = signal(false);

  constructor() {
    this.load();
  }

  private msg(e: HttpErrorResponse, fallback: string) {
    return e.status === 0 ? 'Cannot reach the server. Try again.' : (e.error?.message ?? fallback);
  }

  url(p: string | null) {
    return p ? this.host + p : '';
  }

  pages() {
    return Math.max(1, Math.ceil(this.total() / this.pageSize));
  }

  load() {
    this.loading.set(true);
    this.error.set(null);

    let params = new HttpParams().set('page', this.page()).set('pageSize', this.pageSize);
    if (this.search().trim()) params = params.set('search', this.search().trim());
    if (this.status()) params = params.set('status', this.status());

    this.http.get<{ items: Row[]; total: number }>(this.base, { params }).subscribe({
      next: (r) => {
        this.rows.set(r.items);
        this.total.set(r.total);
        this.loading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(this.msg(e, 'Could not load employees.'));
      },
    });
  }

  onStatus(e: Event) {
    this.status.set((e.target as HTMLSelectElement).value);
    this.page.set(1);
    this.load();
  }

  onSearch(e: Event) {
    this.search.set((e.target as HTMLInputElement).value);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => {
      this.page.set(1);
      this.load();
    }, 350);
  }

  go(p: number) {
    if (p < 1 || p > this.pages() || p === this.page()) return;
    this.page.set(p);
    this.load();
  }

  toggle(r: Row) {
    this.busyId.set(r.id);
    this.error.set(null);

    this.http
      .patch<{ isActive: boolean }>(`${this.base}/${r.id}/status`, { isActive: !r.isActive })
      .subscribe({
        next: (res) => {
          this.rows.update((l) =>
            l.map((x) => (x.id === r.id ? { ...x, isActive: res.isActive } : x)),
          );
          this.busyId.set(null);
        },
        error: (e: HttpErrorResponse) => {
          this.busyId.set(null);
          this.error.set(this.msg(e, 'Could not change status.'));
        },
      });
  }

  remove(r: Row) {
    if (!confirm(`Delete employee "${r.name}" (${r.employeeCode})? This also deletes the login.`))
      return;

    this.busyId.set(r.id);
    this.error.set(null);

    this.http.delete(`${this.base}/${r.id}`).subscribe({
      next: () => {
        this.busyId.set(null);
        this.total.update((t) => t - 1);
        this.rows.update((l) => l.filter((x) => x.id !== r.id));
        if (this.rows().length === 0 && this.page() > 1) this.go(this.page() - 1);
      },
      error: (e: HttpErrorResponse) => {
        this.busyId.set(null);
        this.error.set(this.msg(e, 'Could not delete employee.'));
      },
    });
  }

  view(r: Row) {
    this.detail.set(null);
    this.detailLoading.set(true);
    this.http.get<Detail>(`${this.base}/${r.id}`).subscribe({
      next: (d) => {
        this.detail.set(d);
        this.detailLoading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.detailLoading.set(false);
        this.error.set(this.msg(e, 'Could not load employee.'));
      },
    });
  }

  closeView() {
    this.detail.set(null);
    this.detailLoading.set(false);
  }

  fields(d: Detail): [string, string][] {
    const all: [string, string | number | null][] = [
      ['Employee ID', d.employeeCode],
      ['Role', d.role],
      ['Work type', d.workType ? (this.workLabel[d.workType] ?? d.workType) : null],
      ['Email', d.email],
      ['Official mail', d.officialEmail],
      ['Phone', d.phone],
      ['Emergency', d.emergencyNumber],
      ['Date of birth', d.dateOfBirth],
      ['Gender', d.gender],
      ['Blood group', d.bloodGroup],
      ['Marital status', d.maritalStatus],
      ['Nationality', d.nationality],
      ['Aadhar', d.aadharNumber],
      ['PAN', d.panNumber],
      ['Qualification', d.highestQualification],
      ['Institution', d.institution],
      ['Year of passing', d.yearOfPassing],
      ['Address', d.address],
    ];
    return all
      .filter(([, v]) => v !== null && v !== '')
      .map(([k, v]) => [k, String(v)] as [string, string]);
  }
}
