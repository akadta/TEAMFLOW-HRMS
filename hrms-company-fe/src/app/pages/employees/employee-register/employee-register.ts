import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal, computed } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { BrandService } from '../../../core/brand/brand.service';
import {
  CompanyManager,
  EmployeeManagerPicker,
} from '../employee-manager-picker/employee-manager-picker';

interface RoleOption {
  id: number;
  name: string;
  isActive: boolean;
}
interface DocRow {
  uid: number;
  type: string;
  title: string;
  description: string;
  file: File | null;
}
interface HistRow {
  uid: number;
  employer: string;
  startDate: string;
  endDate: string;
  reason: string;
  file: File | null;
}
type Tab = 'basic' | 'personal' | 'professional' | 'designation' | 'history' | 'security';

const emptyForm = () => ({
  employeeCode: '',
  name: '',
  email: '',
  phoneNumber: '',
  emergencyNumber: '',
  officialEmail: '',
  dateOfBirth: '',
  gender: '',
  bloodGroup: '',
  maritalStatus: '',
  nationality: '',
  aadharNumber: '',
  panNumber: '',
  address: '',
  highestQualification: '',
  institution: '',
  yearOfPassing: '',
  companyRoleId: '',
  workType: '',
  password: '',
  confirmPassword: '',
});
type Form = ReturnType<typeof emptyForm>;

const newDoc = (): DocRow => ({
  uid: 0,
  type: 'Qualification Certificate',
  title: '',
  description: '',
  file: null,
});
const newHist = (): HistRow => ({
  uid: 0,
  employer: '',
  startDate: '',
  endDate: '',
  reason: '',
  file: null,
});
const n = (v: string) => {
  const t = v.trim();
  return t ? t : null;
};
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const MB = 1024 * 1024;

@Component({
  selector: 'app-employee-register',
  imports: [EmployeeManagerPicker],
  templateUrl: './employee-register.html',
  styleUrl: '../employee-form.css',
})
export class EmployeeRegister {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/company/employee-registration`;
  private uid = 0;
  readonly brand = inject(BrandService);

  readonly tabs: { id: Tab; label: string }[] = [
    { id: 'basic', label: 'Basic Info' },
    { id: 'personal', label: 'Personal' },
    { id: 'professional', label: 'Professional' },
    { id: 'designation', label: 'Designation' },
    { id: 'history', label: 'History' },
    { id: 'security', label: 'Security' },
  ];
  readonly genders = ['Male', 'Female', 'Other'];
  readonly bloodGroups = ['A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-'];
  readonly maritals = ['Single', 'Married', 'Divorced', 'Widowed'];
  readonly docTypes = [
    'Qualification Certificate',
    'Technical Certification',
    'Achievement',
    'Research',
    'Other',
  ];
  readonly reasons = [
    'Resignation',
    'Better Opportunity',
    'Contract Ended',
    'Terminated',
    'Relocation',
    'Higher Studies',
    'Other',
  ];
  readonly workTypes = [
    { id: 'office', label: 'In Office' },
    { id: 'remote', label: 'Work From Home' },
    { id: 'hybrid', label: 'Hybrid' },
  ];

  roles = signal<RoleOption[]>([]);
  form = signal<Form>(emptyForm());
  tab = signal<Tab>('basic');
  docs = signal<DocRow[]>([]);
  hist = signal<HistRow[]>([]);
  dDraft = signal<DocRow>(newDoc());
  hDraft = signal<HistRow>(newHist());
  profile = signal<File | null>(null);
  preview = signal<string | null>(null);

  saving = signal(false);
  showPw = signal(false);
  error = signal<string | null>(null);
  success = signal<string | null>(null);

  managers = signal<CompanyManager[]>([]);
  managerIds = computed(() => this.managers().map((m) => m.userId));
  pickerOpen = signal(false);
  pickerMsg = signal<string | null>(null);
  constructor() {
    this.http.get<RoleOption[]>(`${this.api}/roles`).subscribe({
      next: (l) => this.roles.set(l.filter((r) => r.isActive)),
      error: () => this.error.set('Could not load roles.'),
    });
    this.autoCode();
  }

  // ---------- helpers ----------
  private msg(e: HttpErrorResponse, fallback: string) {
    return e.status === 0 ? 'Cannot reach the server. Try again.' : (e.error?.message ?? fallback);
  }

  set<K extends keyof Form>(k: K, v: Form[K]) {
    this.form.update((f) => ({ ...f, [k]: v }));
  }

  val(e: Event) {
    return (e.target as HTMLInputElement).value;
  }

  digits(e: Event, max: number) {
    const el = e.target as HTMLInputElement;
    el.value = el.value.replace(/\D/g, '').slice(0, max);
    return el.value;
  }

  upper(e: Event, max: number, extra = false) {
    const el = e.target as HTMLInputElement;
    el.value = el.value
      .toUpperCase()
      .replace(extra ? /[^A-Z0-9\-_/]/g : /[^A-Z0-9]/g, '')
      .slice(0, max);
    return el.value;
  }

  autoCode() {
    this.http.get<{ code: string }>(`${this.api}/next-code`).subscribe({
      next: (r) => this.set('employeeCode', r.code),
      error: (e: HttpErrorResponse) =>
        this.error.set(this.msg(e, 'Could not generate employee ID.')),
    });
  }

  // ---------- profile picture ----------
  private setProfile(f: File | null) {
    const old = this.preview();
    if (old) URL.revokeObjectURL(old);
    this.profile.set(f);
    this.preview.set(f ? URL.createObjectURL(f) : null);
  }

  onProfile(e: Event) {
    const el = e.target as HTMLInputElement;
    const f = el.files?.[0] ?? null;
    if (f && (!f.type.startsWith('image/') || f.size > 3 * MB)) {
      el.value = '';
      this.error.set('Profile picture: image only, max 3 MB.');
      return;
    }
    this.error.set(null);
    this.setProfile(f);
  }

  // ---------- documents ----------
  setD<K extends keyof DocRow>(k: K, v: DocRow[K]) {
    this.dDraft.update((d) => ({ ...d, [k]: v }));
  }

  onDocFile(e: Event) {
    const el = e.target as HTMLInputElement;
    const f = el.files?.[0] ?? null;
    if (f && f.size > 8 * MB) {
      el.value = '';
      this.error.set('File max 8 MB.');
      return;
    }
    this.error.set(null);
    this.setD('file', f);
  }

  addDoc(el: HTMLInputElement) {
    const d = this.dDraft();
    if (!d.title.trim()) {
      this.error.set('Document title is required.');
      return;
    }
    if (this.docs().length >= 30) {
      this.error.set('Max 30 documents.');
      return;
    }
    this.docs.update((l) => [...l, { ...d, title: d.title.trim(), uid: ++this.uid }]);
    this.dDraft.set(newDoc());
    el.value = '';
    this.error.set(null);
  }

  removeDoc(uid: number) {
    this.docs.update((l) => l.filter((x) => x.uid !== uid));
  }

  // ---------- history ----------
  setH<K extends keyof HistRow>(k: K, v: HistRow[K]) {
    this.hDraft.update((d) => ({ ...d, [k]: v }));
  }

  onHistFile(e: Event) {
    const el = e.target as HTMLInputElement;
    const f = el.files?.[0] ?? null;
    if (f && f.size > 8 * MB) {
      el.value = '';
      this.error.set('File max 8 MB.');
      return;
    }
    this.error.set(null);
    this.setH('file', f);
  }

  addHist(el: HTMLInputElement) {
    const h = this.hDraft();
    if (!h.employer.trim()) {
      this.error.set('Previous employer is required.');
      return;
    }
    if (h.startDate && h.endDate && h.endDate < h.startDate) {
      this.error.set('End date is before start date.');
      return;
    }
    if (this.hist().length >= 20) {
      this.error.set('Max 20 employment entries.');
      return;
    }
    this.hist.update((l) => [...l, { ...h, employer: h.employer.trim(), uid: ++this.uid }]);
    this.hDraft.set(newHist());
    el.value = '';
    this.error.set(null);
  }

  removeHist(uid: number) {
    this.hist.update((l) => l.filter((x) => x.uid !== uid));
  }
  addManager(o: CompanyManager) {
    if (this.managers().some((m) => m.userId === o.userId)) return;
    if (this.managers().length >= 10) {
      this.pickerMsg.set('Max 10 reporting managers.');
      return;
    }
    this.pickerMsg.set(null);
    this.managers.update((l) => [...l, o]);
  }

  removeManager(id: number) {
    this.managers.update((l) => l.filter((m) => m.userId !== id));
  }

  // ---------- submit ----------
  private reset() {
    this.form.set(emptyForm());
    this.docs.set([]);
    this.hist.set([]);
    this.dDraft.set(newDoc());
    this.hDraft.set(newHist());
    this.setProfile(null);
    this.showPw.set(false);
    this.tab.set('basic');
    this.managers.set([]);
  }

  submit() {
    if (this.saving()) return;

    const f = this.form();
    const fail = (t: Tab, m: string) => {
      this.tab.set(t);
      this.error.set(m);
    };

    if (!f.name.trim()) return fail('basic', 'Full name is required.');
    if (!EMAIL.test(f.email.trim())) return fail('basic', 'Enter a valid email.');
    if (!/^\d{10}$/.test(f.phoneNumber)) return fail('basic', 'Phone must be 10 digits.');
    if (f.emergencyNumber && !/^\d{10}$/.test(f.emergencyNumber))
      return fail('basic', 'Emergency number must be 10 digits.');
    if (f.officialEmail && !EMAIL.test(f.officialEmail.trim()))
      return fail('basic', 'Enter a valid official email.');
    if (f.aadharNumber && !/^\d{12}$/.test(f.aadharNumber))
      return fail('personal', 'Aadhar must be 12 digits.');
    if (f.panNumber && !/^[A-Z]{5}\d{4}[A-Z]$/.test(f.panNumber))
      return fail('personal', 'PAN format: AAAAA0000A.');
    if (f.password.length < 8) return fail('security', 'Password must be at least 8 characters.');
    if (!/[A-Za-z]/.test(f.password) || !/\d/.test(f.password))
      return fail('security', 'Password needs a letter and a number.');
    if (f.password !== f.confirmPassword) return fail('security', 'Passwords do not match.');

    const docs = this.docs();
    const hist = this.hist();

    const payload = {
      employeeCode: n(f.employeeCode),
      name: f.name.trim(),
      email: f.email.trim(),
      phoneNumber: f.phoneNumber,
      emergencyNumber: n(f.emergencyNumber),
      officialEmail: n(f.officialEmail),
      dateOfBirth: f.dateOfBirth || null,
      gender: n(f.gender),
      bloodGroup: n(f.bloodGroup),
      maritalStatus: n(f.maritalStatus),
      nationality: n(f.nationality),
      aadharNumber: n(f.aadharNumber),
      panNumber: n(f.panNumber),
      address: n(f.address),
      highestQualification: n(f.highestQualification),
      institution: n(f.institution),
      yearOfPassing: f.yearOfPassing ? Number(f.yearOfPassing) : null,
      companyRoleId: f.companyRoleId ? Number(f.companyRoleId) : null,
      workType: n(f.workType),
      password: f.password,
      confirmPassword: f.confirmPassword,
      documents: docs.map((d, i) => ({
        type: d.type,
        title: d.title,
        description: n(d.description),
        fileKey: d.file ? `doc_${i}` : null,
      })),
      histories: hist.map((h, i) => ({
        employer: h.employer,
        startDate: h.startDate || null,
        endDate: h.endDate || null,
        reasonForLeaving: n(h.reason),
        fileKey: h.file ? `hist_${i}` : null,
      })),
      managerUserIds: this.managers().map((m) => m.userId),
    };

    const fd = new FormData();
    fd.append('payload', JSON.stringify(payload));
    const pf = this.profile();
    if (pf) fd.append('profilePicture', pf);
    docs.forEach((d, i) => d.file && fd.append(`doc_${i}`, d.file));
    hist.forEach((h, i) => h.file && fd.append(`hist_${i}`, h.file));

    this.saving.set(true);
    this.error.set(null);
    this.success.set(null);

    this.http.post<{ id: number; userId: number; employeeCode: string }>(this.api, fd).subscribe({
      next: (r) => {
        this.saving.set(false);
        this.success.set(`Employee registered. ID: ${r.employeeCode}`);
        this.reset();
        this.autoCode();
      },
      error: (e: HttpErrorResponse) => {
        this.saving.set(false);
        this.error.set(this.msg(e, 'Could not register employee.'));
      },
    });
  }
}
