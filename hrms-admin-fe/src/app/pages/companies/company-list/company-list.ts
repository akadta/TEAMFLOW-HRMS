import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { BrandLogo, LogoStyle } from '../../../core/company/brand-logo';
import { LogoControls } from '../../../core/company/logo-controls';
import {
  COLOR_KEYS,
  Company,
  CompanyService,
  DEFAULT_THEME,
  DUP_FIELDS,
  FIELDS,
  Field,
  STYLES,
  THEME_PRESETS,
  Theme,
  ThemeStyle,
  frameStyle,
  getCurrentCoords,
  logoProblem,
  panelStyle,
  parseMapsLink,
  parseTheme,
  readable,
} from '../../../core/company/company.service';

const phoneRx = /^\+?[0-9\s-]{7,18}$/;

@Component({
  selector: 'app-company-list',
  imports: [ReactiveFormsModule, RouterLink, BrandLogo, LogoControls],
  templateUrl: './company-list.html',
})
export class CompanyList {
  private fb = inject(FormBuilder);
  private api = inject(CompanyService);

  fields = FIELDS;
  presets = THEME_PRESETS;
  colorKeys = COLOR_KEYS;
  styles = STYLES;
  panel = panelStyle;
  frame = frameStyle;
  textOn = readable;
  themeOf = parseTheme;
  url = (p: string) => this.api.fileUrl(p);

  // list
  items = signal<Company[]>([]);
  total = signal(0);
  page = signal(1);
  pageSize = 10;
  search = '';
  loading = signal(true);
  listError = signal<string | null>(null);
  busyId = signal<number | null>(null);
  private timer?: ReturnType<typeof setTimeout>;

  // edit modal
  editing = signal<Company | null>(null);
  saving = signal(false);
  formError = signal<string | null>(null);
  serverErrors = signal<Record<string, string>>({});
  theme = signal<Theme>({ ...DEFAULT_THEME });

  logoFile: File | null = null;
  logoPreview = signal<string | null>(null);
  logoError = signal<string | null>(null);

  locationOn = signal(false);
  lat = signal<number | null>(null);
  lng = signal<number | null>(null);
  locating = signal(false);
  locationError = signal<string | null>(null);

  form = this.fb.nonNullable.group({
    companyName: ['', [Validators.required, Validators.maxLength(150)]],
    ownerName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email]],
    phoneNumber: ['', [Validators.required, Validators.pattern(phoneRx)]],
    whatsappNumber: ['', [Validators.pattern(phoneRx)]],
    address: [''],
    about: [''],
  });

  constructor() {
    this.load();
  }

  // ---------- list ----------
  load() {
    this.loading.set(true);
    this.listError.set(null);
    this.api.list(this.search, this.page()).subscribe({
      next: (r) => {
        this.items.set(r.items);
        this.total.set(r.total);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.listError.set('Could not load companies.');
      },
    });
  }

  onSearch(v: string) {
    this.search = v;
    clearTimeout(this.timer);
    this.timer = setTimeout(() => {
      this.page.set(1);
      this.load();
    }, 350);
  }

  pages() {
    return Math.max(1, Math.ceil(this.total() / this.pageSize));
  }

  go(delta: number) {
    this.page.update((p) => p + delta);
    this.load();
  }

  toggle(c: Company) {
    this.busyId.set(c.id);
    this.listError.set(null);
    this.api.setStatus(c.id, !c.isActive).subscribe({
      next: (r) => {
        this.items.update((l) =>
          l.map((x) => (x.id === c.id ? { ...x, isActive: r.isActive } : x)),
        );
        this.busyId.set(null);
      },
      error: () => {
        this.busyId.set(null);
        this.listError.set('Could not change status.');
      },
    });
  }

  // ---------- modal ----------
  openEdit(c: Company) {
    this.form.reset({
      companyName: c.name,
      ownerName: c.ownerName,
      email: c.email,
      phoneNumber: c.phoneNumber ?? '',
      whatsappNumber: c.whatsappNumber ?? '',
      address: c.address ?? '',
      about: c.about ?? '',
    });
    this.theme.set(parseTheme(c.theme));
    this.lat.set(c.latitude);
    this.lng.set(c.longitude);
    this.locationOn.set(c.latitude !== null && c.longitude !== null);
    this.locationError.set(null);
    this.logoFile = null;
    this.logoError.set(null);
    this.logoPreview.set(c.logo ? this.api.fileUrl(c.logo) : null);
    this.serverErrors.set({});
    this.formError.set(null);
    this.editing.set(c);
  }

  close() {
    this.editing.set(null);
  }

  initial() {
    return (this.form.controls.companyName.value.trim()[0] ?? 'C').toUpperCase();
  }

  // ---------- validation ----------
  bad(f: string) {
    return !!this.err(f);
  }

  err(f: string): string | null {
    const s = this.serverErrors()[f];
    if (s) return s;
    const c = this.form.get(f);
    if (!c || !c.touched || !c.errors) return null;
    if (c.errors['required']) return 'Required.';
    if (c.errors['email']) return 'Enter a valid email.';
    if (c.errors['pattern']) return 'Enter a valid number (7-15 digits).';
    return 'Invalid.';
  }

  private setErr(f: string, msg: string | null) {
    this.serverErrors.update((e) => {
      const n = { ...e };
      if (msg) n[f] = msg;
      else delete n[f];
      return n;
    });
  }

  clearErr(f: string) {
    if (this.serverErrors()[f]) this.setErr(f, null);
  }

  checkDup(f: string) {
    if (!DUP_FIELDS.includes(f)) return;
    const c = this.form.get(f);
    const v = (c?.value ?? '').trim();
    if (!v || c?.invalid) return;
    this.api.check(f as Field, v, this.editing()?.id).subscribe({
      next: (r) => this.setErr(f, r.taken ? r.message : null),
    });
  }

  // ---------- logo ----------
  onLogo(e: Event) {
    const file = (e.target as HTMLInputElement).files?.[0];
    if (!file) return;
    const problem = logoProblem(file);
    this.logoError.set(problem);
    if (problem) return;
    this.logoFile = file;
    this.logoPreview.set(URL.createObjectURL(file));
  }

  // ---------- theme ----------
  setColor(key: 'sidebar' | 'navbar' | 'content' | 'accent', value: string) {
    this.theme.update((t) => ({ ...t, [key]: value }));
  }

  setStyle(style: ThemeStyle) {
    this.theme.update((t) => ({ ...t, style }));
  }

  applyPreset(t: Omit<Theme, 'logo'>) {
    this.theme.update((cur) => ({ ...t, logo: cur.logo }));
  }

  setLogo(logo: LogoStyle) {
    this.theme.update((t) => ({ ...t, logo }));
  }
  // ---------- location ----------
  toggleLocation() {
    this.locationOn.update((v) => !v);
    if (!this.locationOn()) {
      this.lat.set(null);
      this.lng.set(null);
      this.locationError.set(null);
    }
  }

  async useCurrentLocation() {
    this.locating.set(true);
    this.locationError.set(null);
    try {
      const p = await getCurrentCoords();
      this.lat.set(p.lat);
      this.lng.set(p.lng);
    } catch {
      this.locationError.set(
        'Could not get your location. Allow permission (needs https) or paste a Google Maps link.',
      );
    }
    this.locating.set(false);
  }

  onLink(v: string) {
    if (!v.trim()) {
      this.locationError.set(null);
      return;
    }
    const p = parseMapsLink(v);
    if (p) {
      this.lat.set(p.lat);
      this.lng.set(p.lng);
      this.locationError.set(null);
    } else {
      this.locationError.set(
        'No coordinates found. Paste the full Google Maps URL (not a short link).',
      );
    }
  }

  // ---------- save ----------
  save() {
    const c = this.editing();
    if (!c) return;
    this.formError.set(null);

    if (this.form.invalid || Object.keys(this.serverErrors()).length > 0) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    const fd = new FormData();
    Object.entries({
      companyName: v.companyName.trim(),
      ownerName: v.ownerName.trim(),
      email: v.email.trim(),
      phoneNumber: v.phoneNumber.trim(),
      whatsappNumber: v.whatsappNumber.trim(),
      address: v.address.trim(),
      about: v.about.trim(),
      theme: JSON.stringify(this.theme()),
    }).forEach(([k, val]) => fd.append(k, val));

    if (this.locationOn() && this.lat() !== null && this.lng() !== null) {
      fd.append('latitude', String(this.lat()));
      fd.append('longitude', String(this.lng()));
    }
    if (this.logoFile) fd.append('logo', this.logoFile);

    this.saving.set(true);
    this.api.update(c.id, fd).subscribe({
      next: () => {
        this.saving.set(false);
        this.editing.set(null);
        this.load();
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        if (err.error?.errors) this.serverErrors.set(err.error.errors);
        this.formError.set(
          err.status === 0
            ? 'Cannot reach the server. Try again.'
            : (err.error?.message ?? 'Something went wrong. Try again.'),
        );
      },
    });
  }
}
