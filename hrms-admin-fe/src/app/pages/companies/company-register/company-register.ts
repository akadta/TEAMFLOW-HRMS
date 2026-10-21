import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { BrandLogo, LogoStyle } from '../../../core/company/brand-logo';
import { LogoControls } from '../../../core/company/logo-controls';
import {
  COLOR_KEYS,
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
  readable,
} from '../../../core/company/company.service';

const phoneRx = /^\+?[0-9\s-]{7,18}$/;

const matchPasswords = (g: AbstractControl) =>
  g.get('password')?.value === g.get('confirmPassword')?.value ? null : { mismatch: true };

@Component({
  selector: 'app-company-register',
  imports: [ReactiveFormsModule, BrandLogo, LogoControls],
  templateUrl: './company-register.html',
})
export class CompanyRegister {
  private fb = inject(FormBuilder);
  private api = inject(CompanyService);
  private router = inject(Router);

  fields = FIELDS;
  presets = THEME_PRESETS;
  colorKeys = COLOR_KEYS;
  styles = STYLES;
  panel = panelStyle;
  frame = frameStyle;
  textOn = readable;

  loading = signal(false);
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

  form = this.fb.nonNullable.group(
    {
      companyName: ['', [Validators.required, Validators.maxLength(150)]],
      ownerName: ['', [Validators.required, Validators.maxLength(100)]],
      email: ['', [Validators.required, Validators.email]],
      phoneNumber: ['', [Validators.required, Validators.pattern(phoneRx)]],
      whatsappNumber: ['', [Validators.pattern(phoneRx)]],
      address: [''],
      about: [''],
      password: ['', [Validators.required, Validators.minLength(6)]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: [matchPasswords] },
  );

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
    if (!c || !c.touched) return null;
    if (f === 'confirmPassword' && this.form.errors?.['mismatch']) return "Passwords don't match.";
    if (!c.errors) return null;
    if (c.errors['required']) return 'Required.';
    if (c.errors['email']) return 'Enter a valid email.';
    if (c.errors['pattern']) return 'Enter a valid number (7-15 digits).';
    if (c.errors['minlength']) return 'Min 6 characters.';
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
    this.api.check(f as Field, v).subscribe({
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

  setLogo(logo: LogoStyle) {
    this.theme.update((t) => ({ ...t, logo }));
  }

  // ---------- theme ----------
  setColor(key: 'sidebar' | 'navbar' | 'content' | 'accent', value: string) {
    this.theme.update((t) => ({ ...t, [key]: value }));
  }

  setStyle(style: ThemeStyle) {
    this.theme.update((t) => ({ ...t, style }));
  }

  // presets change colours and style only, the logo settings stay
  applyPreset(t: Omit<Theme, 'logo'>) {
    this.theme.update((cur) => ({ ...t, logo: cur.logo }));
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

  // ---------- submit ----------
  onSubmit() {
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
      password: v.password,
      confirmPassword: v.confirmPassword,
      theme: JSON.stringify(this.theme()),
    }).forEach(([k, val]) => fd.append(k, val));

    if (this.locationOn() && this.lat() !== null && this.lng() !== null) {
      fd.append('latitude', String(this.lat()));
      fd.append('longitude', String(this.lng()));
    }
    if (this.logoFile) fd.append('logo', this.logoFile);

    this.loading.set(true);
    this.api.register(fd).subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigateByUrl('/company/list');
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
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
