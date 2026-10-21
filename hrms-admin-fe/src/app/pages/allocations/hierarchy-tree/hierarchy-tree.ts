import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  NgZone,
  OnDestroy,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { ManagerOption } from '../../../shared/manager-picker/manager-picker';

// ---------- layout constants ----------
const W = 232; // card width
const H = 82; // card height
const PAD = 70;
const MIN_S = 0.06;
const MAX_S = 2.5;
const MAX_NODES = 3000;
const EASE_MS = 70; // smaller = snappier, bigger = floatier

// mini tree (modal)
const MW = 196;
const MH = 62;
const MG = 16;
const MP = 20;
const MV = 64;
const MINI_MAX = 8;

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
interface Connections {
  person: Person;
  managers: ManagerOption[];
  defaultManagerId: number | null;
  inheritedIds: number[];
  reports: Person[];
  reportsTotal: number;
}
interface TNode {
  key: string;
  person: Person;
  children: TNode[];
}
interface Placed {
  key: string;
  person: Person;
  depth: number;
  b: number;
  d: number;
  x: number;
  y: number;
  childCount: number;
  open: boolean;
  limited: boolean;
}
interface Link {
  key: string;
  d: string;
  fromId: number;
  toId: number;
  implicit: boolean;
}
interface MiniNode {
  key: string;
  kind: 'up' | 'self' | 'down';
  o: ManagerOption;
  x: number;
  y: number;
}
interface MiniLink {
  key: string;
  d: string;
  dashed: boolean;
}
interface View {
  s: number;
  x: number;
  y: number;
}
type Orient = 'v' | 'h';
type StatusFilter = 'all' | 'active' | 'inactive';
type Special = '' | 'external' | 'multiple';
type Rel = 'self' | 'up' | 'down' | 'path';

const clamp = (v: number) => Math.min(MAX_S, Math.max(MIN_S, v));

// every upward connection: chosen managers + the company owner (the owner line is only drawn when it adds a path)
const parentsOf = (p: Person): ManagerOption[] => {
  const ex = p.managers;
  if (!p.defaultManager) return ex;
  return ex.some((m) => m.companyId === p.companyId) ? ex : [...ex, p.defaultManager];
};

@Component({
  selector: 'app-hierarchy-tree',
  imports: [RouterLink],
  templateUrl: './hierarchy-tree.html',
  styleUrl: './hierarchy-tree.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HierarchyTree implements OnDestroy {
  private http = inject(HttpClient);
  private zone = inject(NgZone);
  private base = `${environment.apiUrl}/main/reporting-managers`;
  private host = environment.apiUrl.replace(/\/api\/?$/, '');
  private seq = 0;
  private searchTimer: ReturnType<typeof setTimeout> | undefined;
  private pendingLocate: number | null = null;

  // ----- view engine (no Angular change detection while moving) -----
  private cur: View = { s: 1, x: 0, y: 0 }; // what is drawn
  private tgt: View = { s: 1, x: 0, y: 0 }; // where it is heading
  private raf = 0;
  private last = 0;
  private touched = false; // user moved the view -> do not auto re-fit
  private ro?: ResizeObserver;
  private ptrs = new Map<number, { x: number; y: number }>();
  private pinch: { dist: number; mx: number; my: number } | null = null;
  private drag: { x: number; y: number; tx: number; ty: number; moved: boolean } | null = null;
  private justDragged = false;

  private readonly moveFn = (e: PointerEvent) => this.onPointerMove(e);
  private readonly upFn = (e: PointerEvent) => this.onPointerUp(e);

  readonly W = W;
  readonly H = H;
  readonly MW = MW;
  readonly MH = MH;

  vp = viewChild<ElementRef<HTMLDivElement>>('vp');
  canvas = viewChild<ElementRef<HTMLDivElement>>('canvas');
  pctEl = viewChild<ElementRef<HTMLElement>>('pctEl');

  // ---------- data ----------
  companies = signal<CompanyOption[]>([]);
  companyId = signal<number | null>(null); // 0 = all companies
  people = signal<Person[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  // ---------- view ----------
  orient = signal<Orient>('v');
  maxLevel = signal(99);
  collapsed = signal<Set<string>>(new Set());
  panning = signal(false);
  full = signal(false);
  lost = signal(false);
  hoverId = signal<number | null>(null);
  focusId = signal<number | null>(null);

  // ---------- filters ----------
  search = signal('');
  roleFilter = signal('');
  statusFilter = signal<StatusFilter>('all');
  special = signal<Special>('');
  matchIdx = signal(0);

  // ---------- all-companies view ----------
  serverTruncated = signal(false);
  readonly isAll = computed(() => this.companyId() === 0);

  readonly legend = computed(() => {
    const m = new Map<number, string>();
    for (const p of this.people()) m.set(p.companyId, p.companyName);
    return [...m].map(([id, name]) => ({ id, name }));
  });

  // ---------- modal ----------
  connOpen = signal(false);
  conn = signal<Connections | null>(null);
  connLoading = signal(false);
  connError = signal<string | null>(null);
  connStack = signal<number[]>([]);
  showAllReports = signal(false);

  // ---------- derived ----------
  private byId = computed(() => new Map(this.people().map((p) => [p.userId, p] as const)));

  readonly tree = computed(() => {
    const people = this.people();
    const byId = this.byId();
    const kids = new Map<number, Person[]>();
    const roots: Person[] = [];

    for (const p of people) {
      const inside = parentsOf(p).filter((m) => byId.has(m.userId));
      if (inside.length === 0) roots.push(p); // top of this company, or only has other-company managers
      for (const m of inside) {
        const l = kids.get(m.userId) ?? [];
        l.push(p);
        kids.set(m.userId, l);
      }
    }

    const rank = (p: Person) => (p.isMain ? 0 : p.isOwner ? 1 : 2);
    roots.sort((a, b) => rank(a) - rank(b)); // main team first, then owners

    let budget = MAX_NODES;
    let truncated = false;

    const build = (p: Person, parentKey: string, path: Set<number>): TNode => {
      budget--;
      const key = `${parentKey}/${p.userId}`;
      const next = new Set(path).add(p.userId);
      const children: TNode[] = [];
      for (const c of kids.get(p.userId) ?? []) {
        if (next.has(c.userId)) continue; // loop guard
        if (budget <= 0) {
          truncated = true;
          break;
        }
        children.push(build(c, key, next));
      }
      return { key, person: p, children };
    };

    const out: TNode[] = [];
    for (const r of roots) {
      if (budget <= 0) {
        truncated = true;
        break;
      }
      out.push(build(r, '', new Set<number>()));
    }
    return { roots: out, truncated };
  });

  readonly layout = computed(() => {
    const roots = this.tree().roots;
    const collapsed = this.collapsed();
    const maxLevel = this.maxLevel();
    const horiz = this.orient() === 'h';

    const bStep = horiz ? H + 14 : W + 22;
    const dStep = horiz ? W + 80 : H + 66;

    const nodes: Placed[] = [];
    const edges: [Placed, Placed][] = [];
    let cursor = 0;
    let maxDepth = 0;

    const place = (n: TNode, depth: number): Placed => {
      if (depth > maxDepth) maxDepth = depth;
      const canOpen = depth + 1 < maxLevel;
      const open = n.children.length > 0 && canOpen && !collapsed.has(n.key);

      const placed: Placed = {
        key: n.key,
        person: n.person,
        depth,
        b: 0,
        d: depth * dStep,
        x: 0,
        y: 0,
        childCount: n.children.length,
        open,
        limited: n.children.length > 0 && !canOpen,
      };
      nodes.push(placed); // pre-order: parents first

      if (open) {
        const kids = n.children.map((c) => place(c, depth + 1));
        kids.forEach((k) => edges.push([placed, k]));
        placed.b = (kids[0].b + kids[kids.length - 1].b) / 2;
      } else {
        placed.b = cursor;
        cursor += bStep;
      }
      return placed;
    };

    // a gap between company trees in the all-companies view
    const gap = this.companyId() === 0 ? bStep * 0.7 : 0;
    roots.forEach((r, i) => {
      if (i > 0) cursor += gap;
      place(r, 0);
    });

    for (const n of nodes) {
      n.x = PAD + (horiz ? n.d : n.b);
      n.y = PAD + (horiz ? n.b : n.d);
    }

    const links: Link[] = edges.map(([p, c]) => {
      let d: string;
      if (horiz) {
        const x1 = p.x + W;
        const y1 = p.y + H / 2;
        const x2 = c.x;
        const y2 = c.y + H / 2;
        const xm = (x1 + x2) / 2;
        d = `M${x1} ${y1} C${xm} ${y1} ${xm} ${y2} ${x2} ${y2}`;
      } else {
        const x1 = p.x + W / 2;
        const y1 = p.y + H;
        const x2 = c.x + W / 2;
        const y2 = c.y;
        const ym = (y1 + y2) / 2;
        d = `M${x1} ${y1} C${x1} ${ym} ${x2} ${ym} ${x2} ${y2}`;
      }
      return {
        key: `${p.key}>${c.key}`,
        d,
        fromId: p.person.userId,
        toId: c.person.userId,
        implicit: c.person.defaultManager?.userId === p.person.userId,
      };
    });

    const breadth = nodes.length ? cursor - bStep + (horiz ? H : W) : 0;
    const depthExt = nodes.length ? maxDepth * dStep + (horiz ? W : H) : 0;
    const width = nodes.length ? PAD * 2 + (horiz ? depthExt : breadth) : 0;
    const height = nodes.length ? PAD * 2 + (horiz ? breadth : depthExt) : 0;

    return { nodes, links, width, height };
  });

  readonly stats = computed(() => {
    const ps = this.people();
    let external = 0;
    let multi = 0;
    let inactive = 0;
    for (const p of ps) {
      if (p.managers.some((m) => m.companyId !== p.companyId)) external++;
      if (parentsOf(p).length > 1) multi++;
      if (!p.isActive) inactive++;
    }
    let levels = 0;
    const walk = (ns: TNode[], d: number) => {
      for (const n of ns) {
        if (d > levels) levels = d;
        walk(n.children, d + 1);
      }
    };
    walk(this.tree().roots, 1);
    return { people: ps.length, levels, external, multi, inactive };
  });

  readonly roles = computed(() =>
    [
      ...new Set(
        this.people()
          .map((p) => p.role)
          .filter((r): r is string => !!r),
      ),
    ].sort(),
  );

  readonly activeId = computed(() => this.hoverId() ?? this.focusId());

  /** userId -> relation to the hovered / focused person */
  readonly relations = computed(() => {
    const id = this.activeId();
    const map = new Map<number, Rel>();
    if (id === null) return map;

    const byId = this.byId();
    const me = byId.get(id);
    if (!me) return map;

    map.set(id, 'self');

    const ups = parentsOf(me);
    ups.forEach((u) => map.set(u.userId, 'up'));

    for (const p of this.people()) {
      if (parentsOf(p).some((m) => m.userId === id)) map.set(p.userId, 'down');
    }

    // everything above the direct managers
    const seen = new Set<number>([id, ...ups.map((u) => u.userId)]);
    const queue = ups.map((u) => u.userId);
    while (queue.length) {
      const cur = byId.get(queue.shift()!);
      if (!cur) continue;
      for (const m of parentsOf(cur)) {
        if (seen.has(m.userId)) continue;
        seen.add(m.userId);
        map.set(m.userId, 'path');
        queue.push(m.userId);
      }
    }
    return map;
  });

  readonly filtering = computed(
    () =>
      !!(
        this.search().trim() ||
        this.roleFilter() ||
        this.statusFilter() !== 'all' ||
        this.special()
      ),
  );

  readonly matchIds = computed(() => {
    const set = new Set<number>();
    if (!this.filtering()) return set;

    const q = this.search().trim().toLowerCase();
    const role = this.roleFilter();
    const st = this.statusFilter();
    const sp = this.special();

    for (const p of this.people()) {
      if (
        q &&
        !(
          p.name.toLowerCase().includes(q) ||
          p.companyName.toLowerCase().includes(q) ||
          p.email.toLowerCase().includes(q) ||
          (p.code ?? '').toLowerCase().includes(q) ||
          (p.role ?? '').toLowerCase().includes(q)
        )
      )
        continue;
      if (role && p.role !== role) continue;
      if (st === 'active' && !p.isActive) continue;
      if (st === 'inactive' && p.isActive) continue;
      if (sp === 'external' && !p.managers.some((m) => m.companyId !== p.companyId)) continue;
      if (sp === 'multiple' && parentsOf(p).length < 2) continue;
      set.add(p.userId);
    }
    return set;
  });

  /** visible cards that match, one per person, in tree order */
  readonly matchNodes = computed(() => {
    const out: Placed[] = [];
    if (!this.filtering()) return out;
    const ids = this.matchIds();
    const seen = new Set<number>();
    for (const n of this.layout().nodes) {
      const id = n.person.userId;
      if (ids.has(id) && !seen.has(id)) {
        seen.add(id);
        out.push(n);
      }
    }
    return out;
  });

  readonly mini = computed(() => {
    const c = this.conn();
    if (!c) return null;

    const ups = c.managers;
    const downs = c.reports.slice(0, MINI_MAX);
    const hidden = c.reportsTotal - downs.length;
    const downCount = downs.length + (hidden > 0 ? 1 : 0);

    const rowW = (k: number) => (k > 0 ? k * MW + (k - 1) * MG : 0);
    const width = Math.max(rowW(ups.length), rowW(downCount), MW) + MP * 2;
    const mid = (width - MW) / 2;

    const upY = MP;
    const selfY = ups.length ? MP + MH + MV : MP;
    const downY = selfY + MH + MV;
    const height = (downCount ? downY + MH : selfY + MH) + MP;

    const curve = (x1: number, y1: number, x2: number, y2: number) => {
      const ym = (y1 + y2) / 2;
      return `M${x1} ${y1} C${x1} ${ym} ${x2} ${ym} ${x2} ${y2}`;
    };

    const nodes: MiniNode[] = [];
    const links: MiniLink[] = [];

    const upX0 = (width - rowW(ups.length)) / 2;
    ups.forEach((o, i) => {
      const x = upX0 + i * (MW + MG);
      nodes.push({ key: `u${o.userId}`, kind: 'up', o, x, y: upY });
      links.push({
        key: `lu${o.userId}`,
        d: curve(x + MW / 2, upY + MH, mid + MW / 2, selfY),
        dashed: o.userId === c.defaultManagerId || (c.inheritedIds ?? []).includes(o.userId),
      });
    });

    nodes.push({ key: 'self', kind: 'self', o: c.person, x: mid, y: selfY });

    const downX0 = (width - rowW(downCount)) / 2;
    downs.forEach((o, i) => {
      const x = downX0 + i * (MW + MG);
      nodes.push({ key: `d${o.userId}`, kind: 'down', o, x, y: downY });
      links.push({
        key: `ld${o.userId}`,
        d: curve(mid + MW / 2, selfY + MH, x + MW / 2, downY),
        dashed:
          o.defaultManager?.userId === c.person.userId ||
          (o.inheritedMain ?? []).some((m) => m.userId === c.person.userId),
      });
    });

    let more: { x: number; y: number; label: string } | null = null;
    if (hidden > 0) {
      const x = downX0 + downs.length * (MW + MG);
      more = { x, y: downY, label: `+${hidden} more` };
      links.push({
        key: 'lmore',
        d: curve(mid + MW / 2, selfY + MH, x + MW / 2, downY),
        dashed: true,
      });
    }

    return { width, height, nodes, links, more };
  });

  constructor() {
    // listeners live outside Angular so moving the tree never triggers change detection
    this.zone.runOutsideAngular(() => {
      window.addEventListener('pointermove', this.moveFn);
      window.addEventListener('pointerup', this.upFn);
      window.addEventListener('pointercancel', this.upFn);
    });

    effect((onCleanup) => {
      const el = this.vp()?.nativeElement;
      if (!el) return;

      const wheel = (e: WheelEvent) => this.onWheel(e);
      const down = (e: PointerEvent) => this.onPointerDown(e);

      this.zone.runOutsideAngular(() => {
        el.addEventListener('wheel', wheel, { passive: false });
        el.addEventListener('pointerdown', down);
        if (typeof ResizeObserver !== 'undefined') {
          this.ro = new ResizeObserver(() => this.onResize());
          this.ro.observe(el);
        }
      });

      onCleanup(() => {
        el.removeEventListener('wheel', wheel);
        el.removeEventListener('pointerdown', down);
        this.ro?.disconnect();
      });
    });

    this.http.get<CompanyOption[]>(`${environment.apiUrl}/main/company-roles/companies`).subscribe({
      next: (l) => {
        this.companies.set(l);
        if (l.length) {
          this.companyId.set(0); // 0 = All companies
          this.load();
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

  ngOnDestroy() {
    clearTimeout(this.searchTimer);
    cancelAnimationFrame(this.raf);
    window.removeEventListener('pointermove', this.moveFn);
    window.removeEventListener('pointerup', this.upFn);
    window.removeEventListener('pointercancel', this.upFn);
    this.ro?.disconnect();
    document.body.style.overflow = '';
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

  /** first manager that belongs to another company */
  extCompany(p: Person) {
    return p.managers.find((m) => m.companyId !== p.companyId)?.companyName ?? null;
  }

  /** all upward connections, including the automatic company owner */
  parentCount(p: Person) {
    return parentsOf(p).length;
  }

  hue(id: number) {
    return (id * 137) % 360;
  }

  pickCompanyChip(name: string) {
    this.search.set(name);
    this.matchIdx.set(0);
    setTimeout(() => this.gotoMatch(0), 0);
  }

  // ---------- load ----------
  load() {
    const cid = this.companyId();
    if (cid === null) return;

    const my = ++this.seq;
    this.loading.set(true);
    this.error.set(null);
    this.focusId.set(null);
    this.hoverId.set(null);

    let params = new HttpParams();
    if (cid !== 0) params = params.set('companyId', cid); // 0 = all companies

    this.http
      .get<{ people: Person[]; truncated?: boolean }>(`${this.base}/tree`, { params })
      .subscribe({
        next: (r) => {
          if (my !== this.seq) return;
          this.people.set(r.people);
          this.serverTruncated.set(!!r.truncated);
          // big "all companies" view: start with owners + first level only
          this.collapsed.set(cid === 0 && r.people.length > 150 ? this.deepKeys() : new Set());
          this.loading.set(false);
          this.touched = false;
          setTimeout(() => {
            const pl = this.pendingLocate;
            this.pendingLocate = null;
            if (pl !== null) this.focusUser(pl);
            else this.home(false);
          }, 0);
        },
        error: (e: HttpErrorResponse) => {
          if (my !== this.seq) return;
          this.loading.set(false);
          this.error.set(this.msg(e, 'Could not load the hierarchy.'));
        },
      });
  }

  onCompany(e: Event) {
    const v = Number((e.target as HTMLSelectElement).value);
    this.companyId.set(Number.isNaN(v) ? null : v);
    this.clearFilters();
    this.load();
  }

  // ---------- view controls ----------
  setOrient(o: Orient) {
    if (this.orient() === o) return;
    this.orient.set(o);
    this.touched = false;
    setTimeout(() => this.home(), 0);
  }

  onLevels(e: Event) {
    this.maxLevel.set(Number((e.target as HTMLSelectElement).value) || 99);
    this.touched = false;
    setTimeout(() => this.home(), 0);
  }

  toggle(key: string, ev: Event) {
    ev.stopPropagation();
    this.collapsed.update((s) => {
      const n = new Set(s);
      if (n.has(key)) n.delete(key);
      else n.add(key);
      return n;
    });
  }

  expandAll() {
    this.collapsed.set(new Set());
    this.maxLevel.set(99);
    this.touched = false;
    setTimeout(() => this.home(), 0);
  }

  private deepKeys(): Set<string> {
    // everything below the first level is collapsed
    const keys = new Set<string>();
    const walk = (ns: TNode[], d: number) => {
      for (const n of ns) {
        if (n.children.length && d >= 1) keys.add(n.key);
        walk(n.children, d + 1);
      }
    };
    walk(this.tree().roots, 0);
    return keys;
  }

  collapseAll() {
    this.collapsed.set(this.deepKeys());
    this.touched = false;
    setTimeout(() => this.home(), 0);
  }

  toggleFull() {
    const on = !this.full();
    this.full.set(on);
    document.body.style.overflow = on ? 'hidden' : '';
    this.touched = false; // the resize observer will re-fit
  }

  // ---------- view engine ----------
  private kick() {
    if (this.raf) return;
    this.zone.runOutsideAngular(() => {
      this.raf = requestAnimationFrame((t) => this.tick(t));
    });
  }

  private tick(t: number) {
    this.raf = 0;
    const dt = this.last ? Math.min(64, t - this.last) : 16;
    this.last = t;

    const c = this.cur;
    const g = this.tgt;
    const k = 1 - Math.exp(-dt / EASE_MS); // frame-rate independent easing
    c.s += (g.s - c.s) * k;
    c.x += (g.x - c.x) * k;
    c.y += (g.y - c.y) * k;

    const done =
      Math.abs(g.s - c.s) < 0.0004 && Math.abs(g.x - c.x) < 0.25 && Math.abs(g.y - c.y) < 0.25;
    if (done) {
      c.s = g.s;
      c.x = g.x;
      c.y = g.y;
      this.last = 0;
    }

    this.paint();
    if (!done) this.kick();
  }

  private paint() {
    const c = this.cur;

    const cv = this.canvas()?.nativeElement;
    if (cv) cv.style.transform = `translate3d(${c.x}px,${c.y}px,0) scale(${c.s})`;

    const vp = this.vp()?.nativeElement;
    if (vp) {
      const g = 22 * c.s;
      vp.style.backgroundSize = `${g}px ${g}px`;
      vp.style.backgroundPosition = `${c.x}px ${c.y}px`;
    }

    const pe = this.pctEl()?.nativeElement;
    if (pe) pe.textContent = `${Math.round(c.s * 100)}%`;

    this.checkLost(vp);
  }

  private checkLost(vp?: HTMLElement) {
    if (!vp) return;
    const L = this.layout();
    let lost = false;
    if (L.nodes.length) {
      const c = this.cur;
      const w = vp.clientWidth;
      const h = vp.clientHeight;
      const m = 48;
      lost = c.x + L.width * c.s < m || c.x > w - m || c.y + L.height * c.s < m || c.y > h - m;
    }
    if (lost !== this.lost()) this.lost.set(lost);
  }

  private setTarget(s: number, x: number, y: number, animate: boolean) {
    this.tgt = { s, x, y };
    if (!animate) this.cur = { ...this.tgt };
    this.kick();
  }

  private fitScale(vp: HTMLElement) {
    const L = this.layout();
    const pad = 28;
    return clamp(
      Math.min((vp.clientWidth - pad * 2) / L.width, (vp.clientHeight - pad * 2) / L.height, 1),
    );
  }

  /** the whole tree, centred */
  fit(animate = true) {
    const vp = this.vp()?.nativeElement;
    const L = this.layout();
    if (!vp || !L.nodes.length) return;

    const s = this.fitScale(vp);
    this.touched = false;
    this.setTarget(
      s,
      (vp.clientWidth - L.width * s) / 2,
      Math.max(28, (vp.clientHeight - L.height * s) / 2),
      animate,
    );
  }

  /** smart default: fit the tree, but on a phone with a wide tree start on the top person, readable */
  home(animate = true) {
    const vp = this.vp()?.nativeElement;
    const L = this.layout();
    if (!vp || !L.nodes.length) return;

    const fs = this.fitScale(vp);
    if (vp.clientWidth < 640 && fs < 0.5) {
      const root = L.nodes[0];
      const s = 0.62;
      this.touched = false;
      if (this.orient() === 'h') {
        this.setTarget(s, 20 - root.x * s, vp.clientHeight / 2 - (root.y + H / 2) * s, animate);
      } else {
        this.setTarget(s, vp.clientWidth / 2 - (root.x + W / 2) * s, 20 - root.y * s, animate);
      }
      return;
    }
    this.fit(animate);
  }

  centerOn(n: Placed, minScale = 0.8) {
    const vp = this.vp()?.nativeElement;
    if (!vp) return;
    const s = Math.max(this.cur.s, minScale);
    this.touched = true;
    this.setTarget(
      s,
      vp.clientWidth / 2 - (n.x + W / 2) * s,
      vp.clientHeight / 2 - (n.y + H / 2) * s,
      true,
    );
  }

  private onResize() {
    if (!this.touched) this.home(false);
    else this.kick(); // just repaint + out-of-view check
  }

  // ---------- zoom ----------
  /** eased zoom: moves the target, the loop glides there */
  private zoomTarget(f: number, cx: number, cy: number) {
    const g = this.tgt;
    const s1 = clamp(g.s * f);
    if (s1 === g.s) return;
    const k = s1 / g.s;
    g.x = cx - (cx - g.x) * k;
    g.y = cy - (cy - g.y) * k;
    g.s = s1;
    this.touched = true;
    this.kick();
  }

  /** instant zoom (pinch follows the fingers exactly) */
  private zoomNow(f: number, cx: number, cy: number) {
    const c = this.cur;
    const s1 = clamp(c.s * f);
    if (s1 === c.s) return;
    const k = s1 / c.s;
    c.x = cx - (cx - c.x) * k;
    c.y = cy - (cy - c.y) * k;
    c.s = s1;
    this.tgt = { ...c };
    this.touched = true;
    this.kick();
  }

  zoomBtn(f: number) {
    const vp = this.vp()?.nativeElement;
    if (!vp) return;
    this.zoomTarget(f, vp.clientWidth / 2, vp.clientHeight / 2);
  }

  private onWheel(e: WheelEvent) {
    const vp = this.vp()?.nativeElement;
    if (!vp) return;

    // small screens: let the page scroll unless the user pinches (ctrl) or is in full screen
    if (vp.clientWidth < 640 && !e.ctrlKey && !e.metaKey && !this.full()) return;

    e.preventDefault();
    const r = vp.getBoundingClientRect();
    const delta = e.deltaMode === 1 ? e.deltaY * 16 : e.deltaY;
    const factor = Math.exp(-delta * (e.ctrlKey ? 0.012 : 0.0018));
    this.zoomTarget(factor, e.clientX - r.left, e.clientY - r.top);
  }

  // ---------- pan / pinch ----------
  private onPointerDown(e: PointerEvent) {
    if (e.pointerType === 'mouse' && e.button !== 0) return;
    if ((e.target as HTMLElement).closest('[data-nopan]')) return;

    this.tgt = { ...this.cur }; // stop any glide under the finger
    this.ptrs.set(e.pointerId, { x: e.clientX, y: e.clientY });

    if (this.ptrs.size === 1) {
      this.drag = { x: e.clientX, y: e.clientY, tx: this.cur.x, ty: this.cur.y, moved: false };
      this.panning.set(true);
    } else if (this.ptrs.size === 2) {
      const [a, b] = [...this.ptrs.values()];
      this.pinch = {
        dist: Math.hypot(a.x - b.x, a.y - b.y) || 1,
        mx: (a.x + b.x) / 2,
        my: (a.y + b.y) / 2,
      };
      if (this.drag) this.drag.moved = true; // never counts as a card click
    }
  }

  private onPointerMove(e: PointerEvent) {
    const p = this.ptrs.get(e.pointerId);
    if (!p) return;
    p.x = e.clientX;
    p.y = e.clientY;

    const vp = this.vp()?.nativeElement;
    if (!vp) return;

    if (this.ptrs.size >= 2 && this.pinch) {
      const [a, b] = [...this.ptrs.values()];
      const dist = Math.hypot(a.x - b.x, a.y - b.y) || 1;
      const mx = (a.x + b.x) / 2;
      const my = (a.y + b.y) / 2;
      const r = vp.getBoundingClientRect();

      this.cur.x += mx - this.pinch.mx; // two fingers also move the tree
      this.cur.y += my - this.pinch.my;
      this.zoomNow(dist / this.pinch.dist, mx - r.left, my - r.top);

      this.pinch = { dist, mx, my };
      return;
    }

    const d = this.drag;
    if (!d) return;
    const dx = e.clientX - d.x;
    const dy = e.clientY - d.y;
    if (!d.moved && Math.abs(dx) + Math.abs(dy) > 4) d.moved = true;
    if (d.moved) {
      this.cur.x = d.tx + dx;
      this.cur.y = d.ty + dy;
      this.tgt = { ...this.cur };
      this.touched = true;
      this.kick();
    }
  }

  private onPointerUp(e: PointerEvent) {
    if (!this.ptrs.has(e.pointerId)) return;
    this.ptrs.delete(e.pointerId);

    if (this.ptrs.size >= 2) return;

    if (this.ptrs.size === 1) {
      // pinch ended, one finger left: keep panning from it
      this.pinch = null;
      const [p] = [...this.ptrs.values()];
      this.drag = { x: p.x, y: p.y, tx: this.cur.x, ty: this.cur.y, moved: true };
      return;
    }

    this.pinch = null;
    const moved = this.drag?.moved ?? false;
    this.drag = null;
    this.panning.set(false);
    if (moved) {
      this.justDragged = true;
      setTimeout(() => (this.justDragged = false), 80);
    }
  }

  @HostListener('window:keydown.escape')
  onEsc() {
    if (this.connOpen()) this.closeConn();
    else if (this.full()) this.toggleFull();
  }

  onBgClick(ev: MouseEvent) {
    if (this.justDragged) return;
    if ((ev.target as HTMLElement).closest('[data-nopan],[data-card]')) return;
    this.focusId.set(null);
  }

  // ---------- card styling ----------
  nodeClass(n: Placed): string {
    const p = n.person;
    const rel = this.relations().get(p.userId);
    const focusing = this.activeId() !== null;
    const filtering = this.filtering();
    const match = !filtering || this.matchIds().has(p.userId);

    const c: string[] = [];
    c.push(
      p.isOwner ? 'border-violet-300 bg-gradient-to-br from-violet-50 to-white' : 'border-white/80',
    );
    if (!p.isActive) c.push('grayscale-[40%]');

    if (rel === 'self') c.push('ring-2 ring-blue-500 shadow-2xl z-20');
    else if (rel === 'up') c.push('ring-2 ring-violet-400 z-10');
    else if (rel === 'down') c.push('ring-2 ring-emerald-400 z-10');
    else if (rel === 'path') c.push('ring-1 ring-violet-300');

    if (filtering && match)
      c.push('ring-2 ring-amber-400 shadow-[0_0_0_5px_rgba(251,191,36,0.25)]');
    if (filtering && !match) c.push('opacity-30');
    else if (focusing && !rel) c.push('opacity-50');

    return c.join(' ');
  }

  linkOn(l: Link): boolean {
    const rel = this.relations();
    if (!rel.size) return false;
    const pr = rel.get(l.fromId);
    const cr = rel.get(l.toId);
    if (pr === 'self' && cr === 'down') return true;
    const chain = (r: Rel | undefined) => r === 'self' || r === 'up' || r === 'path';
    return chain(cr) && (pr === 'up' || pr === 'path');
  }

  miniClass(kind: 'up' | 'self' | 'down') {
    if (kind === 'self')
      return 'border-blue-400 ring-2 ring-blue-200 bg-gradient-to-br from-blue-50 to-white';
    if (kind === 'up')
      return 'border-violet-200 bg-white cursor-pointer hover:ring-2 hover:ring-violet-300';
    return 'border-emerald-200 bg-white cursor-pointer hover:ring-2 hover:ring-emerald-300';
  }

  // ---------- filters ----------
  onSearch(e: Event) {
    this.search.set((e.target as HTMLInputElement).value);
    this.matchIdx.set(0);
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      if (this.search().trim()) this.gotoMatch(0);
    }, 350);
  }

  onRole(e: Event) {
    this.roleFilter.set((e.target as HTMLSelectElement).value);
    this.matchIdx.set(0);
  }

  onStatus(e: Event) {
    this.statusFilter.set((e.target as HTMLSelectElement).value as StatusFilter);
    this.matchIdx.set(0);
  }

  onSpecial(e: Event) {
    this.special.set((e.target as HTMLSelectElement).value as Special);
    this.matchIdx.set(0);
  }

  clearFilters() {
    this.search.set('');
    this.roleFilter.set('');
    this.statusFilter.set('all');
    this.special.set('');
    this.matchIdx.set(0);
  }

  gotoMatch(i: number) {
    const list = this.matchNodes();
    if (!list.length) return;
    const idx = ((i % list.length) + list.length) % list.length;
    this.matchIdx.set(idx);
    this.focusId.set(list[idx].person.userId);
    this.centerOn(list[idx]);
  }

  // ---------- cards -> modal ----------
  onNodeClick(n: Placed) {
    if (this.justDragged) return;
    this.focusId.set(n.person.userId);
    this.connStack.set([]);
    this.openConn(n.person.userId);
  }

  openConn(userId: number, push = true) {
    this.connOpen.set(true);
    this.connLoading.set(true);
    this.connError.set(null);
    this.showAllReports.set(false);
    if (push) this.connStack.update((s) => [...s, userId]);

    this.http.get<Connections>(`${this.base}/connections/${userId}`).subscribe({
      next: (c) => {
        const s = this.connStack();
        if (s[s.length - 1] !== userId) return; // a newer click won
        this.conn.set(c);
        this.connLoading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.connLoading.set(false);
        this.connError.set(this.msg(e, 'Could not load connections.'));
      },
    });
  }

  back() {
    const s = this.connStack();
    if (s.length < 2) return;
    const next = s.slice(0, -1);
    this.connStack.set(next);
    this.openConn(next[next.length - 1], false);
  }

  closeConn() {
    this.connOpen.set(false);
    this.conn.set(null);
    this.connStack.set([]);
    this.connError.set(null);
  }

  /** open the person in the tree, switching company if needed */
  locate(o: ManagerOption) {
    this.closeConn();
    const cid = this.companyId();
    if (cid !== 0 && o.companyId !== cid) {
      this.pendingLocate = o.userId;
      this.companyId.set(o.companyId);
      this.clearFilters();
      this.load();
      return;
    }
    this.focusUser(o.userId);
  }

  private focusUser(userId: number) {
    this.collapsed.set(new Set());
    this.maxLevel.set(99);
    this.focusId.set(userId);
    setTimeout(() => {
      const n = this.layout().nodes.find((x) => x.person.userId === userId);
      if (n) this.centerOn(n, 0.9);
      else this.home(false);
    }, 0);
  }
}
