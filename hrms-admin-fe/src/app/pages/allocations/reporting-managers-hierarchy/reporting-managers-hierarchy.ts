import { NgTemplateOutlet } from '@angular/common';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { ManagerOption, ManagerPicker } from '../../../shared/manager-picker/manager-picker';

interface CompanyOption {
  id: number;
  name: string;
  isActive: boolean;
}
interface Person extends ManagerOption {
  managers: ManagerOption[];
  defaultManager: ManagerOption | null;
  reportsCount: number;
  inheritedMain: ManagerOption[];
  blockedMain: ManagerOption[];
}
interface TreeNode {
  key: string;
  person: Person;
  children: TreeNode[];
}
type View = 'list' | 'tree';

@Component({
  selector: 'app-reporting-managers-hierarchy',
  imports: [NgTemplateOutlet, ManagerPicker],
  templateUrl: './reporting-managers-hierarchy.html',
  styleUrl: './reporting-managers-hierarchy.css',
})
export class ReportingManagersHierarchy {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/main/reporting-managers`;
  private host = environment.apiUrl.replace(/\/api\/?$/, '');
  private timer: ReturnType<typeof setTimeout> | undefined;
  private seq = 0;

  readonly pageSize = 50;

  companies = signal<CompanyOption[]>([]);
  companyId = signal<number | null>(null);
  view = signal<View>('list');
  search = signal('');
  page = signal(1);
  total = signal(0);

  people = signal<Person[]>([]); // list view (current page)
  treePeople = signal<Person[]>([]); // tree view (whole company)
  collapsed = signal<Set<string>>(new Set());

  loading = signal(true);
  busy = signal(false);
  error = signal<string | null>(null);

  managing = signal<Person | null>(null);
  pickerOpen = signal(false);

  pages = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize)));
  managingIds = computed(() => (this.managing()?.managers ?? []).map((m) => m.userId));
  roots = computed(() => this.buildTree(this.treePeople()));

  constructor() {
    this.http.get<CompanyOption[]>(`${environment.apiUrl}/main/company-roles/companies`).subscribe({
      next: (l) => {
        this.companies.set(l);
        const first = l.find((c) => c.isActive) ?? l[0];
        if (first) {
          this.companyId.set(first.id);
          this.refresh();
        } else {
          this.loading.set(false);
        }
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Could not load companies.');
      },
    });
  }

  // ---------- helpers ----------
  private msg(e: HttpErrorResponse, fallback: string) {
    return e.status === 0 ? 'Cannot reach the server. Try again.' : (e.error?.message ?? fallback);
  }

  url(p: string | null) {
    return p ? this.host + p : '';
  }

  initial(n: string) {
    return n.charAt(0).toUpperCase();
  }

  /** computed from the CURRENT company, so it changes as soon as someone is moved */
  isExternal(m: ManagerOption, p: Person) {
    return m.companyId !== p.companyId;
  }

  // ---------- load ----------
  refresh(silent = false) {
    if (this.view() === 'list') this.load(silent);
    else this.loadTree(silent);
  }

  private load(silent: boolean) {
    const cid = this.companyId();
    if (cid === null) return;

    const my = ++this.seq;
    if (!silent) this.loading.set(true);
    this.error.set(null);

    let params = new HttpParams()
      .set('companyId', cid)
      .set('page', this.page())
      .set('pageSize', this.pageSize);
    if (this.search().trim()) params = params.set('search', this.search().trim());

    this.http
      .get<{ items: Person[]; total: number }>(`${this.base}/overview`, { params })
      .subscribe({
        next: (r) => {
          if (my !== this.seq) return;
          this.people.set(r.items);
          this.total.set(r.total);
          this.loading.set(false);
        },
        error: (e: HttpErrorResponse) => {
          if (my !== this.seq) return;
          this.loading.set(false);
          this.error.set(this.msg(e, 'Could not load employees.'));
        },
      });
  }

  private loadTree(silent: boolean) {
    const cid = this.companyId();
    if (cid === null) return;

    const my = ++this.seq;
    if (!silent) this.loading.set(true);
    this.error.set(null);

    this.http
      .get<{ people: Person[] }>(`${this.base}/tree`, {
        params: new HttpParams().set('companyId', cid),
      })
      .subscribe({
        next: (r) => {
          if (my !== this.seq) return;
          this.treePeople.set(r.people);
          this.loading.set(false);
        },
        error: (e: HttpErrorResponse) => {
          if (my !== this.seq) return;
          this.loading.set(false);
          this.error.set(this.msg(e, 'Could not load hierarchy.'));
        },
      });
  }

  setView(v: View) {
    if (this.view() === v) return;
    this.view.set(v);
    this.refresh();
  }

  onCompany(e: Event) {
    const v = (e.target as HTMLSelectElement).value;
    this.companyId.set(v === '' ? null : Number(v));
    this.page.set(1);
    this.refresh();
  }

  onSearch(e: Event) {
    this.search.set((e.target as HTMLInputElement).value);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => {
      this.page.set(1);
      this.load(false);
    }, 350);
  }

  go(p: number) {
    if (p < 1 || p > this.pages() || p === this.page()) return;
    this.page.set(p);
    this.load(false);
  }

  // ---------- tree ----------
  private buildTree(people: Person[]): TreeNode[] {
    const byId = new Map(people.map((p) => [p.userId, p] as const));
    const kids = new Map<number, Person[]>();
    const roots: Person[] = [];

    for (const p of people) {
      const sameCo = p.managers.some((m) => m.companyId === p.companyId);
      const parents = p.defaultManager && !sameCo ? [...p.managers, p.defaultManager] : p.managers;
      const inside = parents.filter((m) => byId.has(m.userId));

      // nobody above them in THIS company (e.g. the owner, or only external managers) -> root
      if (inside.length === 0) roots.push(p);

      for (const m of inside) {
        const l = kids.get(m.userId) ?? [];
        l.push(p);
        kids.set(m.userId, l);
      }
    }

    roots.sort((a, b) => Number(b.isOwner) - Number(a.isOwner));

    const build = (p: Person, parentKey: string, path: Set<number>): TreeNode => {
      const key = `${parentKey}/${p.userId}`;
      const next = new Set(path).add(p.userId);
      return {
        key,
        person: p,
        children: (kids.get(p.userId) ?? [])
          .filter((c) => !next.has(c.userId))
          .map((c) => build(c, key, next)),
      };
    };

    return roots.map((r) => build(r, '', new Set<number>()));
  }

  isOpen(key: string) {
    return !this.collapsed().has(key);
  }

  toggle(key: string) {
    this.collapsed.update((s) => {
      const n = new Set(s);
      if (n.has(key)) n.delete(key);
      else n.add(key);
      return n;
    });
  }

  expandAll() {
    this.collapsed.set(new Set());
  }

  collapseAll() {
    const keys = new Set<string>();
    const walk = (nodes: TreeNode[]) =>
      nodes.forEach((n) => {
        if (n.children.length) keys.add(n.key);
        walk(n.children);
      });
    walk(this.roots());
    this.collapsed.set(keys);
  }

  // ---------- manage ----------
  openManage(p: Person) {
    this.error.set(null);
    this.managing.set(p);
  }

  closeManage() {
    this.managing.set(null);
    this.pickerOpen.set(false);
    this.error.set(null);
  }

  private applyUpdate(u: Person) {
    this.busy.set(false);
    this.managing.set(u);
    this.people.update((l) => l.map((x) => (x.userId === u.userId ? u : x)));
    this.treePeople.update((l) => l.map((x) => (x.userId === u.userId ? u : x)));
    this.refresh(true); // refresh report counts of the managers
  }

  addManager(o: ManagerOption) {
    const p = this.managing();
    if (!p || this.busy()) return;

    this.busy.set(true);
    this.error.set(null);

    this.http
      .post<Person>(`${this.base}/${p.userId}/managers`, { managerUserId: o.userId })
      .subscribe({
        next: (u) => this.applyUpdate(u),
        error: (e: HttpErrorResponse) => {
          this.busy.set(false);
          this.error.set(this.msg(e, 'Could not add manager.'));
        },
      });
  }

  removeManager(m: ManagerOption) {
    const p = this.managing();
    if (!p || this.busy()) return;

    this.busy.set(true);
    this.error.set(null);

    this.http.delete<Person>(`${this.base}/${p.userId}/managers/${m.userId}`).subscribe({
      next: (u) => this.applyUpdate(u),
      error: (e: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(this.msg(e, 'Could not remove manager.'));
      },
    });
  }

  hasMain(p: Person) {
    return p.managers.some((m) => m.isMain);
  }

  exclude(m: ManagerOption) {
    this.changeAccess('post', m);
  }

  restore(m: ManagerOption) {
    this.changeAccess('delete', m);
  }

  private changeAccess(kind: 'post' | 'delete', m: ManagerOption) {
    const p = this.managing();
    if (!p || this.busy()) return;

    this.busy.set(true);
    this.error.set(null);

    const url = `${this.base}/${p.userId}/exclusions`;
    const call$ =
      kind === 'post'
        ? this.http.post<Person>(url, { managerUserId: m.userId })
        : this.http.delete<Person>(`${url}/${m.userId}`);

    call$.subscribe({
      next: (u) => this.applyUpdate(u),
      error: (e: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(this.msg(e, 'Could not change access.'));
      },
    });
  }
}
