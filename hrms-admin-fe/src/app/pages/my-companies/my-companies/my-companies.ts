import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { CompanyCard, errMsg, fileUrl } from '../mc.util';

@Component({
  selector: 'app-my-companies',
  templateUrl: './my-companies.html',
})
export class MyCompanies {
  private http = inject(HttpClient);
  private router = inject(Router);
  private data = inject(ActivatedRoute).snapshot.data;

  // set per route: where a click goes, and the page title
  readonly base: string = this.data['base'] ?? 'my-companies';
  readonly title: string = this.data['title'] ?? 'My company';

  readonly fileUrl = fileUrl;
  readonly tabs = [
    { id: 'all', label: 'All' },
    { id: 'active', label: 'Active' },
    { id: 'inactive', label: 'Inactive' },
  ];
  readonly sorts = [
    { id: 'name', label: 'Name A–Z' },
    { id: 'nameDesc', label: 'Name Z–A' },
    { id: 'most', label: 'Most employees' },
    { id: 'fewest', label: 'Fewest employees' },
    { id: 'new', label: 'Newest first' },
    { id: 'old', label: 'Oldest first' },
  ];
  readonly inp =
    'rounded-lg border border-slate-200 bg-white/80 px-2.5 py-1.5 text-xs text-slate-900 outline-none transition focus:border-blue-400 focus:ring-2 focus:ring-blue-200';

  items = signal<CompanyCard[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  search = signal('');
  status = signal('all');
  minEmp = signal('');
  maxEmp = signal('');
  sort = signal('name');

  shown = computed(() => {
    const q = this.search().trim().toLowerCase();
    const st = this.status();
    const mn = this.minEmp() === '' ? null : Number(this.minEmp());
    const mx = this.maxEmp() === '' ? null : Number(this.maxEmp());

    const list = this.items().filter(
      (c) =>
        (st === 'all' || (st === 'active') === c.isActive) &&
        (mn === null || c.employees >= mn) &&
        (mx === null || c.employees <= mx) &&
        (!q || `${c.name} ${c.ownerName}`.toLowerCase().includes(q)),
    );

    const s = this.sort();
    return [...list].sort((a, b) =>
      s === 'nameDesc'
        ? b.name.localeCompare(a.name)
        : s === 'most'
          ? b.employees - a.employees
          : s === 'fewest'
            ? a.employees - b.employees
            : s === 'new'
              ? b.created.localeCompare(a.created)
              : s === 'old'
                ? a.created.localeCompare(b.created)
                : a.name.localeCompare(b.name),
    );
  });

  constructor() {
    this.http.get<CompanyCard[]>(`${environment.apiUrl}/main/my-companies`).subscribe({
      next: (l) => {
        this.items.set(l);
        this.loading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(errMsg(e, 'Could not load companies.'));
      },
    });
  }

  val(e: Event) {
    return (e.target as HTMLInputElement).value;
  }

  reset() {
    this.search.set('');
    this.status.set('all');
    this.minEmp.set('');
    this.maxEmp.set('');
    this.sort.set('name');
  }

  open(c: CompanyCard) {
    this.router.navigate(['/' + this.base, c.id]);
  }
}
