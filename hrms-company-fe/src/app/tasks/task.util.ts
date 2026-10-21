export const TASK_STATUSES = ['todo', 'in_progress', 'on_hold', 'done'];

export const TASK_STATUS: Record<string, { label: string; cls: string }> = {
  todo: { label: 'To do', cls: 'bg-slate-200 text-slate-700' },
  in_progress: { label: 'In progress', cls: 'bg-blue-100 text-blue-700' },
  on_hold: { label: 'On hold', cls: 'bg-amber-100 text-amber-700' },
  done: { label: 'Done', cls: 'bg-emerald-100 text-emerald-700' },
};

export interface TaskFile {
  id: number;
  fileName: string;
  filePath: string;
  size: number;
}

export interface SelfTask {
  id: number;
  title: string;
  description: string | null;
  status: string;
  createdAt: string;
  updatedAt: string;
  completedAt: string | null;
  files: TaskFile[];
}

export const fmtSize = (b: number) =>
  b < 1024
    ? `${b} B`
    : b < 1048576
      ? `${Math.round(b / 1024)} KB`
      : `${(b / 1048576).toFixed(1)} MB`;

export interface Approval {
  id: number;
  group: string;
  mode: string;
  decision: string;
  approver: string | null;
  note: string | null;
  decidedAt: string | null;
}

export interface DailyTask {
  id: number;
  companyId: number;
  company: string | null;
  name: string;
  code: string | null;
  kind: string;
  title: string;
  description: string | null;
  status: string;
  progress: number;
  startAt: string | null;
  dueAt: string | null;
  createdAt: string;
  updatedAt: string;
  completedAt: string | null;
  approval: string;
  approvalNote: string | null;
  files: TaskFile[];
  approvals: Approval[];
  canDecide: boolean;
}

export const APPROVAL: Record<string, { label: string; cls: string }> = {
  none: { label: '—', cls: 'text-slate-400' },
  pending: { label: 'Waiting approval', cls: 'bg-violet-100 text-violet-700' },
  approved: { label: 'Approved', cls: 'bg-emerald-100 text-emerald-700' },
  rejected: { label: 'Rejected', cls: 'bg-red-100 text-red-700' },
};

export const GROUP: Record<string, string> = {
  managers: 'Reporting manager',
  owner: 'Owner',
  main: 'Main team',
  selected: 'Selected',
};

const ext = (p: string) => p.split('.').pop()?.toLowerCase() ?? '';
export const isImage = (p: string) => ['jpg', 'jpeg', 'png', 'gif', 'webp', 'bmp'].includes(ext(p));
export const isVideo = (p: string) => ['mp4', 'webm', 'mov', 'm4v', 'ogg', 'mkv'].includes(ext(p));

// ISO (UTC) -> "2026-10-12T10:30" in IST, for <input type="datetime-local">
export const toLocalInput = (iso: string | null) =>
  iso
    ? new Date(iso)
        .toLocaleString('sv-SE', { timeZone: 'Asia/Kolkata' })
        .replace(' ', 'T')
        .slice(0, 16)
    : '';

// "2026-10-12T10:30" (IST) -> ISO with offset
export const toIso = (local: string) => (local ? `${local}:00+05:30` : '');

export const overdue = (t: { dueAt: string | null; status: string }) =>
  !!t.dueAt && t.status !== 'done' && Date.parse(t.dueAt) < Date.now();

export const ASSIGN_STATUS: Record<string, { label: string; cls: string }> = {
  awaiting_owner: { label: 'Waiting for owner', cls: 'bg-violet-100 text-violet-700' },
  scheduled: { label: 'Scheduled', cls: 'bg-amber-100 text-amber-700' },
  sent: { label: 'Sent', cls: 'bg-emerald-100 text-emerald-700' },
  declined: { label: 'Declined', cls: 'bg-red-100 text-red-700' },
  cancelled: { label: 'Cancelled', cls: 'bg-slate-200 text-slate-600' },
};

export interface AssignTarget {
  userId: number;
  name: string;
  code: string | null;
  status: string | null;
  progress: number;
  approval: string | null;
}

export interface AssignItem {
  id: number;
  companyId: number;
  company: string | null;
  source: string;
  by: string;
  title: string;
  description: string | null;
  dueAt: string | null;
  sendAt: string | null;
  status: string;
  decisionNote: string | null;
  createdAt: string;
  sentAt: string | null;
  files: { fileName: string; filePath: string; size: number }[];
  targets: AssignTarget[];
}
