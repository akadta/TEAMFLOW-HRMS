import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, OnDestroy, OnInit, inject, input, output, signal } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface ManagerOption {
  userId: number;
  employeeId: number | null;
  name: string;
  email: string;
  code: string | null;
  role: string | null;
  profilePicture: string | null;
  companyId: number;
  companyName: string;
  isOwner: boolean;
  isActive: boolean;
  isMain: boolean;
}

interface CompanyOption {
  id: number;
  name: string;
  isActive: boolean;
}

@Component({
  selector: 'app-manager-picker',
  templateUrl: './manager-picker.html',
})
export class ManagerPicker implements OnInit, OnDestroy {
  private http = inject(HttpClient);
  private api = environment.apiUrl;
  private host = environment.apiUrl.replace(/\/api\/?$/, '');
  private timer: ReturnType<typeof setTimeout> | undefined;
  private seq = 0;

  /** the person's own company: opened first, and used to flag "other company" */
  companyId = input<number | null>(null);
  /** the person being edited (cannot be their own manager) */
  excludeUserId = input<number | null>(null);
  /** managers already chosen */
  selectedIds = input<number[]>([]);
  /** message from the parent (e.g. server error) */
  message = input<string | null>(null);
  /** only the main team (used by the main employee form) */
  mainOnly = input(false);
  /** set when the person being edited is the owner of this company */
  ownerOfCompanyId = input<number | null>(null);

  ownerBlocked() {
    return this.ownerOfCompanyId() !== null && this.company() === this.ownerOfCompanyId();
  }
  picked = output<ManagerOption>();
  closed = output<void>();

  companies = signal<CompanyOption[]>([]);
  company = signal<number | null>(null);
  search = signal('');
  items = signal<ManagerOption[]>([]);
  loading = signal(false);
  error = signal<string | null>(null);

  ngOnInit() {
    this.company.set(this.mainOnly() ? 0 : this.companyId());
    this.http.get<CompanyOption[]>(`${this.api}/main/company-roles/companies`).subscribe({
      next: (l) => this.companies.set(l.filter((c) => c.isActive || c.id === this.companyId())),
      error: () => this.error.set('Could not load companies.'),
    });

    this.load();
  }

  ngOnDestroy() {
    clearTimeout(this.timer);
  }

  url(p: string | null) {
    return p ? this.host + p : '';
  }

  initial(n: string) {
    return n.charAt(0).toUpperCase();
  }

  isSelected(id: number) {
    return this.selectedIds().includes(id);
  }

  isOther() {
    return !this.mainOnly() && this.company() !== this.companyId();
  }
  onCompany(e: Event) {
    const v = (e.target as HTMLSelectElement).value;
    this.company.set(v === '' ? null : Number(v));
    this.load();
  }

  onSearch(e: Event) {
    this.search.set((e.target as HTMLInputElement).value);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.load(), 300);
  }

  private load() {
    const cid = this.company();
    if (cid === null) {
      this.items.set([]);
      return;
    }

    const my = ++this.seq; // ignore out-of-order responses
    this.loading.set(true);
    this.error.set(null);

    let params = new HttpParams().set('companyId', cid);
    if (this.search().trim()) params = params.set('search', this.search().trim());
    const ex = this.excludeUserId();
    if (ex) params = params.set('excludeUserId', ex);

    this.http
      .get<ManagerOption[]>(`${this.api}/main/reporting-managers/candidates`, { params })
      .subscribe({
        next: (l) => {
          if (my !== this.seq) return;
          // the owner of the person's own company is already the automatic default
          this.items.set(l.filter((o) => !(o.isOwner && o.companyId === this.companyId())));
          this.loading.set(false);
        },
        error: (e: HttpErrorResponse) => {
          if (my !== this.seq) return;
          this.loading.set(false);
          this.error.set(
            e.status === 0
              ? 'Cannot reach the server.'
              : (e.error?.message ?? 'Could not load people.'),
          );
        },
      });
  }
}
