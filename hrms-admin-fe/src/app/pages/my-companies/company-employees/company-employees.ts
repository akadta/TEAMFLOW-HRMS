import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { EmpItem, errMsg, fileUrl, istDate } from '../mc.util';

interface Detail {
  employee: {
    id: number;
    userId: number;
    employeeCode: string;
    name: string;
    email: string;
    phone: string | null;
    officialEmail: string | null;
    emergencyNumber: string | null;
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
    workType: string | null;
    role: string | null;
    isActive: boolean;
    joined: string;
    company: string;
    documents: {
      type: string;
      title: string;
      description: string | null;
      filePath: string | null;
    }[];
    histories: {
      employer: string;
      startDate: string | null;
      endDate: string | null;
      reasonForLeaving: string | null;
      relievingLetter: string | null;
    }[];
  };
  managers: string[];
  owner: string;
}

const fmt = (v: string | null) =>
  v
    ? new Date(v.length === 10 ? v + 'T00:00:00Z' : v).toLocaleDateString('en-IN', {
        timeZone: v.length === 10 ? 'UTC' : 'Asia/Kolkata',
        day: '2-digit',
        month: 'short',
        year: 'numeric',
      })
    : null;

@Component({
  selector: 'app-company-employees',
  templateUrl: './company-employees.html',
})
export class CompanyEmployees {
  private http = inject(HttpClient);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private cid = Number(this.route.snapshot.paramMap.get('companyId'));

  // set per route: where "back" and a row click go; open = rows are clickable
  readonly base: string = this.route.snapshot.data['base'] ?? 'my-companies';
  readonly clickable: boolean = !!this.route.snapshot.data['open'];

  readonly fileUrl = fileUrl;
  readonly tabs = [
    { id: 'all', label: 'All' },
    { id: 'active', label: 'Active' },
    { id: 'inactive', label: 'Inactive' },
  ];
  readonly sorts = [
    { id: 'name', label: 'Name A–Z' },
    { id: 'nameDesc', label: 'Name Z–A' },
    { id: 'code', label: 'Employee ID' },
    { id: 'new', label: 'Newest joined' },
    { id: 'old', label: 'Oldest joined' },
  ];
  readonly inp =
    'rounded-lg border border-slate-200 bg-white/80 px-2.5 py-1.5 text-xs text-slate-900 outline-none transition focus:border-blue-400 focus:ring-2 focus:ring-blue-200';

  company = signal<{ id: number; name: string; ownerName: string; logo: string | null } | null>(
    null,
  );
  items = signal<EmpItem[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  search = signal('');
  status = signal('all');
  role = signal('');
  work = signal('');
  joinFrom = signal('');
  joinTo = signal('');
  sort = signal('name');

  // details modal
  detailOpen = signal(false);
  detail = signal<Detail | null>(null);
  detailError = signal<string | null>(null);

  sections = computed(() => {
    const d = this.detail();
    if (!d) return [];
    const e = d.employee;
    const items = (x: [string, string | number | null][]) => x;
    return [
      {
        title: 'Basic',
        items: items([
          ['Employee ID', e.employeeCode],
          ['Email', e.email],
          ['Phone', e.phone],
          ['Official mail', e.officialEmail],
          ['Emergency number', e.emergencyNumber],
          ['Role', e.role],
          ['Work type', e.workType],
          ['Joined', fmt(e.joined)],
        ]),
      },
      {
        title: 'Personal',
        items: items([
          ['Date of birth', fmt(e.dateOfBirth)],
          ['Gender', e.gender],
          ['Blood group', e.bloodGroup],
          ['Marital status', e.maritalStatus],
          ['Nationality', e.nationality],
          ['Aadhar', e.aadharNumber],
          ['PAN', e.panNumber],
          ['Address', e.address],
        ]),
      },
      {
        title: 'Professional',
        items: items([
          ['Qualification', e.highestQualification],
          ['Institution', e.institution],
          ['Year of passing', e.yearOfPassing],
          [
            'Reports to',
            d.managers.length ? d.managers.join(', ') : `${d.owner} (company owner, default)`,
          ],
        ]),
      },
    ];
  });

  roles = computed(() =>
    [
      ...new Set(
        this.items()
          .map((e) => e.role)
          .filter((x): x is string => !!x),
      ),
    ].sort(),
  );

  shown = computed(() => {
    const q = this.search().trim().toLowerCase();
    const st = this.status();
    const role = this.role();
    const w = this.work();
    const jf = this.joinFrom();
    const jt = this.joinTo();

    const list = this.items().filter((e) => {
      const joined = istDate(Date.parse(e.joined));
      return (
        (st === 'all' || (st === 'active') === e.isActive) &&
        (!role || e.role === role) &&
        (!w || (w === 'none' ? !e.workType : e.workType === w)) &&
        (!jf || joined >= jf) &&
        (!jt || joined <= jt) &&
        (!q ||
          `${e.name} ${e.employeeCode} ${e.email} ${e.phone ?? ''} ${e.role ?? ''}`
            .toLowerCase()
            .includes(q))
      );
    });

    const s = this.sort();
    return [...list].sort((a, b) =>
      s === 'nameDesc'
        ? b.name.localeCompare(a.name)
        : s === 'code'
          ? a.employeeCode.localeCompare(b.employeeCode)
          : s === 'new'
            ? b.joined.localeCompare(a.joined)
            : s === 'old'
              ? a.joined.localeCompare(b.joined)
              : a.name.localeCompare(b.name),
    );
  });

  constructor() {
    this.http
      .get<{
        company: { id: number; name: string; ownerName: string; logo: string | null };
        items: EmpItem[];
      }>(`${environment.apiUrl}/main/my-companies/${this.cid}/employees`)
      .subscribe({
        next: (r) => {
          this.company.set(r.company);
          this.items.set(r.items);
          this.loading.set(false);
        },
        error: (e: HttpErrorResponse) => {
          this.loading.set(false);
          this.error.set(errMsg(e, 'Could not load employees.'));
        },
      });
  }

  val(e: Event) {
    return (e.target as HTMLInputElement).value;
  }

  reset() {
    this.search.set('');
    this.status.set('all');
    this.role.set('');
    this.work.set('');
    this.joinFrom.set('');
    this.joinTo.set('');
    this.sort.set('name');
  }

  back() {
    this.router.navigate(['/' + this.base]);
  }

  // attendance flow: go to the report. view-only flow: show the full details modal
  open(e: EmpItem) {
    if (this.clickable) {
      this.router.navigate(['/' + this.base, this.cid, 'employee', e.id]);
      return;
    }

    this.detail.set(null);
    this.detailError.set(null);
    this.detailOpen.set(true);
    this.http
      .get<Detail>(`${environment.apiUrl}/main/my-companies/${this.cid}/employees/${e.id}`)
      .subscribe({
        next: (d) => this.detail.set(d),
        error: (x: HttpErrorResponse) => this.detailError.set(errMsg(x, 'Could not load details.')),
      });
  }

  fmt(v: string | null) {
    return fmt(v);
  }
}
