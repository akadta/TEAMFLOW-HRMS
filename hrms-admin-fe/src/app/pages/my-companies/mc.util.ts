import { HttpErrorResponse } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface CompanyCard {
  id: number;
  name: string;
  ownerName: string;
  logo: string | null;
  isActive: boolean;
  employees: number;
  created: string;
}

export interface EmpItem {
  id: number;
  userId: number;
  employeeCode: string;
  name: string;
  email: string;
  phone: string | null;
  role: string | null;
  workType: string | null;
  profilePicture: string | null;
  isActive: boolean;
  joined: string;
}

export interface DayRow {
  date: string;
  status: string;
  note: string | null;
  checkInAt: string | null;
  lateMinutes: number;
  source: string | null;
  lat: number | null;
  lng: number | null;
  photoPath: string | null;
}

export interface ReqItem {
  id: number;
  date: string;
  reason: string;
  status: string;
  lateMinutes: number;
  requestedAt: string;
  decidedAt: string | null;
  decisionNote: string | null;
  decidedBy: string | null;
  files: { fileName: string; filePath: string }[];
}

// late colour bands: green -> red
export const BANDS = [
  { max: 0, label: 'On time', cls: 'bg-emerald-100 text-emerald-700', hex: '#059669' },
  { max: 15, label: '1–15 min', cls: 'bg-lime-100 text-lime-700', hex: '#65a30d' },
  { max: 30, label: '16–30 min', cls: 'bg-amber-100 text-amber-700', hex: '#d97706' },
  { max: 60, label: '31–60 min', cls: 'bg-orange-100 text-orange-700', hex: '#ea580c' },
  { max: Infinity, label: '60+ min', cls: 'bg-red-100 text-red-700', hex: '#dc2626' },
];
export const band = (m: number | null) => BANDS.find((b) => (m ?? 0) <= b.max)!;

export const STATUS: Record<string, { label: string; cls: string; hex: string }> = {
  present: { label: 'Present', cls: 'bg-emerald-100 text-emerald-700', hex: '#059669' },
  late: { label: 'Late', cls: 'bg-orange-100 text-orange-700', hex: '#ea580c' },
  absent: { label: 'Absent', cls: 'bg-red-100 text-red-700', hex: '#dc2626' },
  holiday: { label: 'Holiday', cls: 'bg-sky-100 text-sky-700', hex: '#0284c7' },
  leave: { label: 'Leave', cls: 'bg-teal-100 text-teal-700', hex: '#0d9488' },
  cancelled: { label: 'Cancelled', cls: 'bg-slate-200 text-slate-600', hex: '#64748b' },
  approved: { label: 'Approved', cls: 'bg-emerald-100 text-emerald-700', hex: '#059669' },
  rejected: { label: 'Rejected', cls: 'bg-red-100 text-red-700', hex: '#dc2626' },
  pending: { label: 'Pending', cls: 'bg-amber-100 text-amber-700', hex: '#d97706' },
  notyet: { label: 'Not yet', cls: 'bg-slate-100 text-slate-500', hex: '#94a3b8' },
  na: { label: 'No rule', cls: 'bg-slate-100 text-slate-400', hex: '#94a3b8' },
};

export const DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const IST = 'Asia/Kolkata';

export const lateText = (m: number | null) =>
  !m
    ? 'On time'
    : m < 60
      ? `${m}m late`
      : `${Math.floor(m / 60)}h${m % 60 ? ' ' + (m % 60) + 'm' : ''} late`;

export const t12 = (iso: string | null) =>
  iso
    ? new Date(iso).toLocaleTimeString('en-IN', {
        timeZone: IST,
        hour: 'numeric',
        minute: '2-digit',
        second: '2-digit',
        hour12: true,
      })
    : '—';

export const dt12 = (iso: string | null) =>
  iso
    ? new Date(iso).toLocaleString('en-IN', {
        timeZone: IST,
        day: '2-digit',
        month: 'short',
        hour: 'numeric',
        minute: '2-digit',
        hour12: true,
      })
    : '—';

export const dayLabel = (d: string) => {
  const [y, m, dd] = d.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, dd)).toLocaleDateString('en-IN', {
    timeZone: 'UTC',
    weekday: 'short',
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  });
};

export const istDate = (ms: number) => new Date(ms).toLocaleDateString('en-CA', { timeZone: IST });
export const shift = (iso: string, days: number) => {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, d + days)).toISOString().slice(0, 10);
};
export const dow = (iso: string) => new Date(iso + 'T00:00:00Z').getUTCDay();

export const srcLabel = (s: string | null) =>
  s === 'request' ? 'Request' : s === 'override' ? 'Override' : s ? 'Marked' : '';

export const fileUrl = (p: string) => environment.apiUrl.replace(/\/api\/?$/, '') + p;

export const errMsg = (e: HttpErrorResponse, fallback: string) =>
  e.status === 0 ? 'Cannot reach the server. Try again.' : (e.error?.message ?? fallback);

// ---------- export colours ----------
export const rgb = (hex: string): [number, number, number] => {
  const n = parseInt(hex.slice(1), 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
};

// mix with white: 0 = same colour, 1 = white
export const mixWhite = (hex: string, amt: number): [number, number, number] =>
  rgb(hex).map((c) => Math.round(c + (255 - c) * amt)) as [number, number, number];

export const toHex = (c: [number, number, number]) =>
  c
    .map((x) => x.toString(16).padStart(2, '0'))
    .join('')
    .toUpperCase();

export const textOn = (hex: string) => {
  const [r, g, b] = rgb(hex);
  return 0.299 * r + 0.587 * g + 0.114 * b > 150 ? '#0f172a' : '#ffffff';
};

// "2026-10-09" -> "09 Oct"
export const short = (d: string) => {
  const [y, m, dd] = d.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, dd)).toLocaleDateString('en-IN', {
    timeZone: 'UTC',
    day: '2-digit',
    month: 'short',
  });
};

export const hhmm = (t: string | null) => {
  if (!t) return '—';
  const [h, m] = t.split(':').map(Number);
  return `${h % 12 || 12}:${String(m).padStart(2, '0')} ${h < 12 ? 'AM' : 'PM'}`;
};

// attendance result: only Present / Absent (a pending day counts as Absent until approved)
export const result = (s: string) =>
  s === 'present' ? 'Present' : s === 'absent' || s === 'pending' ? 'Absent' : '—';

export const RESULT_CLS: Record<string, string> = {
  Present: 'bg-emerald-100 text-emerald-700',
  Absent: 'bg-red-100 text-red-700',
  '—': 'text-slate-400',
};

export const mapUrl = (lat: number, lng: number) => `https://www.google.com/maps?q=${lat},${lng}`;
