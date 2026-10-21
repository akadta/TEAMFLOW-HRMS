import { Injectable, computed, effect, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { DEFAULT_LOGO, LogoStyle, parseLogo } from './brand-logo';

export type ThemeStyle = 'glass' | 'neumorphic' | 'solid' | 'gradient';

export interface Theme {
  style: ThemeStyle;
  sidebar: string;
  navbar: string;
  content: string;
  accent: string;
  logo: LogoStyle;
}

export interface Brand {
  companyId: number;
  name: string;
  logo: string | null;
  theme: Theme;
}

const KEY = 'hrms_company_brand';

// shown when the company has not uploaded a logo (file: public/logo/PROXHRMS.png)
export const DEFAULT_LOGO_URL = '/logo/PROXHRMS.png';

export const DEFAULT_THEME: Theme = {
  style: 'glass',
  sidebar: '#1e3a8a',
  navbar: '#ffffff',
  content: '#eaf2ff',
  accent: '#2563eb',
  logo: { ...DEFAULT_LOGO },
};

function parseTheme(json: string | null | undefined): Theme {
  try {
    const t = JSON.parse(json ?? '{}');
    return { ...DEFAULT_THEME, ...t, logo: parseLogo(t.logo) };
  } catch {
    return { ...DEFAULT_THEME, logo: { ...DEFAULT_LOGO } };
  }
}

// text colour that stays readable on a hex background
function readable(hex: string): string {
  const n = parseInt(hex.slice(1), 16);
  const lum = 0.299 * (n >> 16) + 0.587 * ((n >> 8) & 255) + 0.114 * (n & 255);
  return lum > 150 ? '#0f172a' : '#ffffff';
}

// background behind everything (same as the register preview frame)
function frameStyle(t: Theme): Record<string, string> {
  return {
    background: `radial-gradient(circle at 15% 20%, ${t.accent}55, transparent 45%), radial-gradient(circle at 85% 85%, ${t.accent}33, transparent 45%), ${t.content}`,
  };
}

// sidebar / navbar / card look for each style (same as the register preview)
function panelStyle(t: Theme, color: string): Record<string, string> {
  switch (t.style) {
    case 'glass':
      return {
        background: color + 'a6',
        'backdrop-filter': 'blur(14px)',
        '-webkit-backdrop-filter': 'blur(14px)',
        border: '1px solid rgba(255,255,255,0.5)',
        'box-shadow': '0 8px 24px rgba(15,23,42,0.12)',
      };
    case 'neumorphic':
      return {
        background: color,
        'box-shadow': '6px 6px 14px rgba(15,23,42,0.18), -6px -6px 14px rgba(255,255,255,0.75)',
      };
    case 'gradient':
      return { background: `linear-gradient(135deg, ${color}, ${t.accent})` };
    default:
      return { background: color, border: '1px solid rgba(15,23,42,0.08)' };
  }
}

@Injectable({ providedIn: 'root' })
export class BrandService {
  private root = environment.apiUrl.replace(/\/api\/?$/, '');

  // last company that signed in on this device (kept after logout so the login page stays themed)
  readonly brand = signal<Brand | null>(this.load());

  readonly theme = computed(() => this.brand()?.theme ?? DEFAULT_THEME);
  readonly name = computed(() => this.brand()?.name ?? '');
  readonly initial = computed(() => (this.name().trim()[0] ?? 'H').toUpperCase());

  // true when the company uploaded its own logo
  readonly hasCompanyLogo = computed(() => !!this.brand()?.logo);

  // company logo if it exists, otherwise the default PROXHRMS logo
  readonly logoUrl = computed(() => {
    const logo = this.brand()?.logo;
    if (!logo) return DEFAULT_LOGO_URL;
    return /^https?:/i.test(logo) ? logo : this.root + logo;
  });

  // company logo uses its saved style; the default logo is shown bare (no box, no border)
  readonly logoStyle = computed<LogoStyle>(() =>
    this.hasCompanyLogo()
      ? this.theme().logo
      : { ...DEFAULT_LOGO, shape: 'none', fit: 'contain', zoom: 1, x: 0, y: 0 },
  );

  readonly frame = computed(() => frameStyle(this.theme()));
  readonly sidebarStyle = computed(() => panelStyle(this.theme(), this.theme().sidebar));
  readonly navbarStyle = computed(() => panelStyle(this.theme(), this.theme().navbar));

  readonly sidebarText = computed(() => readable(this.theme().sidebar));
  readonly navbarText = computed(() => readable(this.theme().navbar));
  readonly contentText = computed(() => readable(this.theme().content));
  readonly accentText = computed(() => readable(this.theme().accent));

  constructor() {
    // browser tab icon and title follow the company brand
    effect(() => {
      let link = document.querySelector<HTMLLinkElement>('link[rel="icon"]');
      if (!link) {
        link = document.createElement('link');
        link.rel = 'icon';
        document.head.appendChild(link);
      }
      link.removeAttribute('type');
      link.href = this.logoUrl();
      document.title = this.name() || 'HRMS';
    });
  }

  // called after every login / session restore: the newest company always takes over
  set(c: { id: number; name: string; logo: string | null; theme: string }) {
    const b: Brand = { companyId: c.id, name: c.name, logo: c.logo, theme: parseTheme(c.theme) };
    this.brand.set(b);
    try {
      localStorage.setItem(KEY, JSON.stringify(b));
    } catch {
      /* storage blocked: theme still applies for this session */
    }
  }

  private load(): Brand | null {
    try {
      const raw = localStorage.getItem(KEY);
      if (!raw) return null;
      const b = JSON.parse(raw) as Brand;
      if (!b?.name || !b.theme) return null;
      return { ...b, theme: { ...DEFAULT_THEME, ...b.theme, logo: parseLogo(b.theme.logo) } };
    } catch {
      return null;
    }
  }
}
