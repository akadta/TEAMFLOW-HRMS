import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, OnDestroy, OnInit, inject, input, output, signal } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { BrandService } from '../../../core/brand/brand.service';

export interface CompanyManager {
  userId: number;
  employeeId: number | null;
  name: string;
  email: string;
  code: string | null;
  role: string | null;
  profilePicture: string | null;
  isActive: boolean;
}

@Component({
  selector: 'app-employee-manager-picker',
  templateUrl: './employee-manager-picker.html',
  styleUrl: '../employee-form.css',
})
export class EmployeeManagerPicker implements OnInit, OnDestroy {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/company/reporting-managers`;
  private host = environment.apiUrl.replace(/\/api\/?$/, '');
  private timer: ReturnType<typeof setTimeout> | undefined;
  private seq = 0;
  readonly brand = inject(BrandService);

  /** the employee being edited (cannot be their own manager) */
  excludeUserId = input<number | null>(null);
  /** managers already chosen */
  selectedIds = input<number[]>([]);
  /** message from the parent (e.g. "Max 10 reporting managers.") */
  message = input<string | null>(null);

  picked = output<CompanyManager>();
  closed = output<void>();

  search = signal('');
  items = signal<CompanyManager[]>([]);
  loading = signal(false);
  error = signal<string | null>(null);

  ngOnInit() {
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

  onSearch(e: Event) {
    this.search.set((e.target as HTMLInputElement).value);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.load(), 300);
  }

  private load() {
    const my = ++this.seq; // ignore out-of-order responses
    this.loading.set(true);
    this.error.set(null);

    let params = new HttpParams();
    if (this.search().trim()) params = params.set('search', this.search().trim());
    const ex = this.excludeUserId();
    if (ex) params = params.set('excludeUserId', ex);

    this.http.get<CompanyManager[]>(`${this.base}/candidates`, { params }).subscribe({
      next: (l) => {
        if (my !== this.seq) return;
        this.items.set(l);
        this.loading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        if (my !== this.seq) return;
        this.loading.set(false);
        this.error.set(
          e.status === 0 ? 'Cannot reach the server.' : (e.error?.message ?? 'Could not load employees.'),
        );
      },
    });
  }
}