export interface TaskFile {
  id: number;
  fileName: string;
  filePath: string;
  size: number;
}

export interface Approval {
  id: number;
  group: string;
  mode: string;
  decision: string;
  approver: string | null;
  note: string | null;
}

export interface TeamTask {
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
  completedAt: string | null;
  approval: string;
  approvalNote: string | null;
  files: TaskFile[];
  approvals: Approval[];
  canDecide: boolean;
}

export const TASK_STATUSES = ['todo', 'in_progress', 'on_hold', 'done'];

export const TASK_STATUS: Record<string, { label: string; cls: string }> = {
  todo: { label: 'To do', cls: 'bg-slate-200 text-slate-700' },
  in_progress: { label: 'In progress', cls: 'bg-blue-100 text-blue-700' },
  on_hold: { label: 'On hold', cls: 'bg-amber-100 text-amber-700' },
  done: { label: 'Done', cls: 'bg-emerald-100 text-emerald-700' },
};

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

export const overdue = (t: { dueAt: string | null; status: string }) =>
  !!t.dueAt && t.status !== 'done' && Date.parse(t.dueAt) < Date.now();
