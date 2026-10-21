import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { DEFAULT_LOGO, LogoStyle, parseLogo } from './brand-logo';

export type Field = 'companyName' | 'ownerName' | 'email' | 'phoneNumber' | 'whatsappNumber';
export type ThemeStyle = 'glass' | 'neumorphic' | 'solid' | 'gradient';

export interface Theme {
  style: ThemeStyle;
  sidebar: string;
  navbar: string;
  content: string;
  accent: string;
  logo: LogoStyle;
}

export interface Company {
  id: number;
  name: string;
  ownerName: string;
  email: string;
  phoneNumber: string;
  whatsappNumber: string | null;
  logo: string | null;
  theme: string;
  address: string | null;
  latitude: number | null;
  longitude: number | null;
  about: string | null;
  isActive: boolean;
  createdAt: string;
}

export const FIELDS: { name: Field; label: string; type: string; placeholder: string }[] = [
  { name: 'companyName', label: 'Company name', type: 'text', placeholder: 'Acme Pvt Ltd' },
  { name: 'ownerName', label: 'Owner name', type: 'text', placeholder: 'Full name' },
  { name: 'email', label: 'Email', type: 'email', placeholder: 'name@company.com' },
  { name: 'phoneNumber', label: 'Phone number', type: 'tel', placeholder: '+91 98765 43210' },
  {
    name: 'whatsappNumber',
    label: 'WhatsApp number (optional)',
    type: 'tel',
    placeholder: '+91 98765 43210',
  },
];

export const DUP_FIELDS: string[] = ['companyName', 'email', 'phoneNumber', 'whatsappNumber'];

export const STYLES: { key: ThemeStyle; label: string }[] = [
  { key: 'glass', label: 'Glass' },
  { key: 'neumorphic', label: 'Neumorphic' },
  { key: 'solid', label: 'Solid' },
  { key: 'gradient', label: 'Gradient' },
];

export const COLOR_KEYS: { key: 'sidebar' | 'navbar' | 'content' | 'accent'; label: string }[] = [
  { key: 'sidebar', label: 'Sidebar' },
  { key: 'navbar', label: 'Navbar' },
  { key: 'content', label: 'Content' },
  { key: 'accent', label: 'Accent' },
];

export const DEFAULT_THEME: Theme = {
  style: 'glass',
  sidebar: '#1e3a8a',
  navbar: '#ffffff',
  content: '#eaf2ff',
  accent: '#2563eb',
  logo: { ...DEFAULT_LOGO },
};

export const THEME_PRESETS: { name: string; theme: Omit<Theme, 'logo'> }[] = [
  { name: 'Ocean', theme: DEFAULT_THEME },
  {
    name: 'Frost',
    theme: {
      style: 'glass',
      sidebar: '#ffffff',
      navbar: '#ffffff',
      content: '#f1f5f9',
      accent: '#3b82f6',
    },
  },
  {
    name: 'Midnight',
    theme: {
      style: 'solid',
      sidebar: '#0f172a',
      navbar: '#1e293b',
      content: '#0b1220',
      accent: '#38bdf8',
    },
  },
  {
    name: 'Soft',
    theme: {
      style: 'neumorphic',
      sidebar: '#e6edf7',
      navbar: '#e6edf7',
      content: '#e6edf7',
      accent: '#2563eb',
    },
  },
  {
    name: 'Aurora',
    theme: {
      style: 'gradient',
      sidebar: '#1d4ed8',
      navbar: '#0ea5e9',
      content: '#f0f9ff',
      accent: '#6366f1',
    },
  },
];

export function parseTheme(json: string | null): Theme {
  try {
    const t = JSON.parse(json ?? '{}');
    return {
      ...DEFAULT_THEME,
      ...t,
      content: t.content ?? t.navbar ?? DEFAULT_THEME.content,
      logo: parseLogo(t.logo),
    };
  } catch {
    return { ...DEFAULT_THEME, logo: { ...DEFAULT_LOGO } };
  }
}

// text colour that stays readable on a hex background
export function readable(hex: string): string {
  const n = parseInt(hex.slice(1), 16);
  const lum = 0.299 * (n >> 16) + 0.587 * ((n >> 8) & 255) + 0.114 * (n & 255);
  return lum > 150 ? '#0f172a' : '#ffffff';
}

// background of the mini dashboard frame
export function frameStyle(t: Theme): Record<string, string> {
  return {
    background: `radial-gradient(circle at 15% 20%, ${t.accent}55, transparent 45%), radial-gradient(circle at 85% 85%, ${t.accent}33, transparent 45%), ${t.content}`,
  };
}

// sidebar / navbar / card look for each style
export function panelStyle(t: Theme, color: string): Record<string, string> {
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

// fast: network location, cached up to 10 min, 8s timeout
export function getCurrentCoords(): Promise<{ lat: number; lng: number }> {
  return new Promise((resolve, reject) => {
    if (!navigator.geolocation) {
      reject();
      return;
    }
    navigator.geolocation.getCurrentPosition(
      (p) => resolve({ lat: +p.coords.latitude.toFixed(6), lng: +p.coords.longitude.toFixed(6) }),
      () => reject(),
      { enableHighAccuracy: false, timeout: 8000, maximumAge: 600000 },
    );
  });
}

// pulls lat/lng out of a full Google Maps URL
export function parseMapsLink(url: string): { lat: number; lng: number } | null {
  const patterns = [
    /!3d(-?\d+\.\d+)!4d(-?\d+\.\d+)/,
    /[?&](?:q|ll|query|destination|center)=(-?\d+\.\d+)(?:,|%2C)\s*(-?\d+\.\d+)/i,
    /@(-?\d+\.\d+),(-?\d+\.\d+)/,
  ];
  for (const p of patterns) {
    const m = url.match(p);
    if (m) return { lat: +m[1], lng: +m[2] };
  }
  return null;
}

export function logoProblem(f: File): string | null {
  if (!['image/png', 'image/jpeg', 'image/webp'].includes(f.type)) return 'Use PNG, JPG or WebP.';
  if (f.size > 2 * 1024 * 1024) return 'Max size is 2 MB.';
  return null;
}

@Injectable({ providedIn: 'root' })
export class CompanyService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/main/companies`;
  private root = environment.apiUrl.replace(/\/api\/?$/, '');

  fileUrl(path: string) {
    return this.root + path;
  }

  check(field: Field, value: string, excludeId?: number) {
    let params = new HttpParams().set('field', field).set('value', value);
    if (excludeId) params = params.set('excludeId', excludeId);
    return this.http.get<{ taken: boolean; message: string | null }>(`${this.base}/check`, {
      params,
    });
  }

  register(fd: FormData) {
    return this.http.post<{ id: number }>(`${this.base}/register`, fd);
  }

  list(search: string, page: number) {
    const params = new HttpParams().set('search', search).set('page', page).set('pageSize', 10);
    return this.http.get<{ items: Company[]; total: number }>(this.base, { params });
  }

  update(id: number, fd: FormData) {
    return this.http.put(`${this.base}/${id}`, fd);
  }

  setStatus(id: number, isActive: boolean) {
    return this.http.patch<{ isActive: boolean }>(`${this.base}/${id}/status`, { isActive });
  }
}
