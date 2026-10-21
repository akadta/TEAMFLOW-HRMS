export const ASSIGN_STATUS: Record<string, { label: string; cls: string }> = {
  awaiting_owner: { label: 'Waiting for company owner', cls: 'bg-violet-100 text-violet-700' },
  scheduled: { label: 'Scheduled', cls: 'bg-amber-100 text-amber-700' },
  sent: { label: 'Sent to employees', cls: 'bg-emerald-100 text-emerald-700' },
  declined: { label: 'Declined', cls: 'bg-red-100 text-red-700' },
  cancelled: { label: 'Cancelled', cls: 'bg-slate-200 text-slate-600' },
};

export const TASK_STATUS: Record<string, { label: string; cls: string }> = {
  todo: { label: 'To do', cls: 'bg-slate-200 text-slate-700' },
  in_progress: { label: 'In progress', cls: 'bg-blue-100 text-blue-700' },
  on_hold: { label: 'On hold', cls: 'bg-amber-100 text-amber-700' },
  done: { label: 'Done', cls: 'bg-emerald-100 text-emerald-700' },
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

// "2026-10-12T10:30" (IST) -> ISO with offset
export const toIso = (local: string) => (local ? `${local}:00+05:30` : '');

export const fmtSize = (b: number) =>
  b < 1024
    ? `${b} B`
    : b < 1048576
      ? `${Math.round(b / 1024)} KB`
      : `${(b / 1048576).toFixed(1)} MB`;
