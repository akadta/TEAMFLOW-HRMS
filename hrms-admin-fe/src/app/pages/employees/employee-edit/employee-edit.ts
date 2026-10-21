import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, OnDestroy, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { ManagerOption, ManagerPicker } from '../../../shared/manager-picker/manager-picker';

interface CompanyOption {
  id: number;
  name: string;
  isActive: boolean;
}
interface RoleOption {
  id: number;
  name: string;
  isActive: boolean;
}
interface DetailDoc {
  id: number;
  type: string;
  title: string;
  description: string | null;
  filePath: string | null;
}
interface DetailHist {
  id: number;
  employer: string;
  startDate: string | null;
  endDate: string | null;
  reasonForLeaving: string | null;
  relievingLetter: string | null;
}
interface Detail {
  id: number;
  userId: number;
  companyId: number;
  companyName: string;
  employeeCode: string;
  name: string;
  email: string;
  phoneNumber: string | null;
  emergencyNumber: string | null;
  officialEmail: string | null;
  dateOfBirth: string | null;
  gender: string | null;
  bloodGroup: string | null;
  maritalStatus: string | null;
  nationality: string | null;
  aadharNumber: string | null;
  panNumber: string | null;
  address: string | null;
  profilePicture: string | null;
  highestQualification: string | null;
  institution: string | null;
  yearOfPassing: number | null;
  companyRoleId: number | null;
  workType: string | null;
  isActive: boolean;
  documents: DetailDoc[];
  histories: DetailHist[];
}
interface DocRow {
  uid: number;
  id: number | null;
  type: string;
  title: string;
  description: string;
  file: File | null;
  existingPath: string | null;
  removeFile: boolean;
}
interface HistRow {
  uid: number;
  id: number | null;
  employer: string;
  startDate: string;
  endDate: string;
  reason: string;
  file: File | null;
  existingPath: string | null;
  removeFile: boolean;
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
  newPassword: '',
  confirmPassword: '',
});
type Form = ReturnType<typeof emptyForm>;

const newDoc = (): DocRow => ({
  uid: 0,
  id: null,
  type: 'Qualification Certificate',
  title: '',
  description: '',
  file: null,
  existingPath: null,
  removeFile: false,
});
const newHist = (): HistRow => ({
  uid: 0,
  id: null,
  employer: '',
  startDate: '',
  endDate: '',
  reason: '',
  file: null,
  existingPath: null,
  removeFile: false,
});
const n = (v: string) => {
  const t = v.trim();
  return t ? t : null;
};
const s = (v: string | null | undefined) => v ?? '';
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const MB = 1024 * 1024;

@Component({
  selector: 'app-employee-edit',
  imports: [RouterLink, ManagerPicker],
  templateUrl: './employee-edit.html',
  styleUrl: './employee-edit.css',
})
export class EmployeeEdit implements OnDestroy {
  private http = inject(HttpClient);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private api = environment.apiUrl;
  private host = environment.apiUrl.replace(/\/api\/?$/, '');
  private uid = 0;
  private readonly id = Number(this.route.snapshot.paramMap.get('id'));

  readonly tabs: { id: Tab; label: string }[] = [
    { id: 'basic', label: 'Basic Info' },
    { id: 'personal', label: 'Personal' },
    { id: 'professional', label: 'Professional' },
    { id: 'designation', label: 'Designation' },
    { id: 'history', label: 'History' },
    { id: 'security', label: 'Password' },
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
  companies = signal<CompanyOption[]>([]);
  companyId = signal<number | null>(null);
  private orig = { companyId: 0, code: '', roleId: '' };

  companyName = signal('');
  form = signal<Form>(emptyForm());
  tab = signal<Tab>('basic');

  docs = signal<DocRow[]>([]);
  hist = signal<HistRow[]>([]);
  dDraft = signal<DocRow>(newDoc());
  hDraft = signal<HistRow>(newHist());
  editingDoc = signal<number | null>(null);
  editingHist = signal<number | null>(null);

  existingProfile = signal<string | null>(null);
  removeProfile = signal(false);
  profile = signal<File | null>(null);
  preview = signal<string | null>(null);

  loading = signal(true);
  loadError = signal<string | null>(null);
  saving = signal(false);
  showPw = signal(false);
  error = signal<string | null>(null);
  managers = signal<ManagerOption[]>([]);
  managerIds = computed(() => this.managers().map((m) => m.userId));
  pickerOpen = signal(false);
  pickerMsg = signal<string | null>(null);
  selfUserId = signal<number | null>(null);
  managersLoaded = signal(false); // never wipe managers if they failed to load

  constructor() {
    if (!this.id) {
      this.loading.set(false);
      this.loadError.set('Invalid employee.');
      return;
    }

    this.http.get<CompanyOption[]>(`${this.api}/main/company-roles/companies`).subscribe({
      next: (l) => this.companies.set(l),
      error: () => this.error.set('Could not load companies.'),
    });

    this.http.get<Detail>(`${this.api}/main/employee-edit/${this.id}`).subscribe({
      next: (d) => {
        this.fill(d);
        this.selfUserId.set(d.userId);
        this.http
          .get<{ managers: ManagerOption[] }>(
            `${this.api}/main/reporting-managers/person/${d.userId}`,
          )
          .subscribe({
            next: (p) => {
              this.managers.set(p.managers);
              this.managersLoaded.set(true);
            },
            error: () => this.error.set('Could not load reporting managers.'),
          });
        this.companyId.set(d.companyId);
        this.orig = {
          companyId: d.companyId,
          code: d.employeeCode,
          roleId: d.companyRoleId?.toString() ?? '',
        };
        this.loadRoles(d.companyId, d.companyRoleId);
        this.loading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.loadError.set(this.msg(e, 'Could not load employee.'));
      },
    });
  }

  ngOnDestroy() {
    const p = this.preview();
    if (p) URL.revokeObjectURL(p);
  }

  // ---------- helpers ----------
  private msg(e: HttpErrorResponse, fallback: string) {
    return e.status === 0 ? 'Cannot reach the server. Try again.' : (e.error?.message ?? fallback);
  }

  url(p: string | null) {
    return p ? this.host + p : '';
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

  // ---------- load ----------
  private fill(d: Detail) {
    this.companyName.set(d.companyName);
    this.form.set({
      employeeCode: d.employeeCode,
      name: d.name,
      email: d.email,
      phoneNumber: s(d.phoneNumber),
      emergencyNumber: s(d.emergencyNumber),
      officialEmail: s(d.officialEmail),
      dateOfBirth: s(d.dateOfBirth),
      gender: s(d.gender),
      bloodGroup: s(d.bloodGroup),
      maritalStatus: s(d.maritalStatus),
      nationality: s(d.nationality),
      aadharNumber: s(d.aadharNumber),
      panNumber: s(d.panNumber),
      address: s(d.address),
      highestQualification: s(d.highestQualification),
      institution: s(d.institution),
      yearOfPassing: d.yearOfPassing?.toString() ?? '',
      companyRoleId: d.companyRoleId?.toString() ?? '',
      workType: s(d.workType),
      newPassword: '',
      confirmPassword: '',
    });
    this.existingProfile.set(d.profilePicture);
    this.docs.set(
      d.documents.map((x) => ({
        uid: ++this.uid,
        id: x.id,
        type: x.type,
        title: x.title,
        description: s(x.description),
        file: null,
        existingPath: x.filePath,
        removeFile: false,
      })),
    );
    this.hist.set(
      d.histories.map((x) => ({
        uid: ++this.uid,
        id: x.id,
        employer: x.employer,
        startDate: s(x.startDate),
        endDate: s(x.endDate),
        reason: s(x.reasonForLeaving),
        file: null,
        existingPath: x.relievingLetter,
        removeFile: false,
      })),
    );
  }

  private loadRoles(companyId: number, currentRoleId: number | null) {
    this.http
      .get<RoleOption[]>(`${this.api}/main/company-roles`, { params: { companyId } })
      .subscribe({
        next: (l) => this.roles.set(l.filter((r) => r.isActive || r.id === currentRoleId)),
        error: () => this.error.set('Could not load roles.'),
      });
  }

  // ---------- company ----------
  onPickCompany(e: Event) {
    const id = Number((e.target as HTMLSelectElement).value);
    if (!id || id === this.companyId()) return;

    this.error.set(null);
    this.companyId.set(id);
    this.companyName.set(this.companies().find((c) => c.id === id)?.name ?? '');
    this.roles.set([]);

    if (id === this.orig.companyId) {
      // back to the original company: restore ID and role
      this.set('employeeCode', this.orig.code);
      this.set('companyRoleId', this.orig.roleId);
      this.loadRoles(id, this.orig.roleId ? Number(this.orig.roleId) : null);
    } else {
      this.set('companyRoleId', '');
      this.loadRoles(id, null);
      this.autoCode();
    }
  }

  autoCode() {
    const id = this.companyId();
    if (id === null) return;
    this.http
      .get<{ code: string }>(`${this.api}/main/employee-registration/next-code`, {
        params: { companyId: id },
      })
      .subscribe({
        next: (r) => this.set('employeeCode', r.code),
        error: (e: HttpErrorResponse) =>
          this.error.set(this.msg(e, 'Could not generate employee ID.')),
      });
  }

  // ---------- profile picture ----------
  avatarSrc() {
    const p = this.preview();
    if (p) return p;
    const e = this.existingProfile();
    return e && !this.removeProfile() ? this.url(e) : null;
  }

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
    if (f) this.removeProfile.set(false);
  }

  clearProfile(input: HTMLInputElement) {
    input.value = '';
    this.setProfile(null);
    this.removeProfile.set(true);
  }

  isExternal(m: ManagerOption) {
    return m.companyId !== this.companyId();
  }

  addManager(o: ManagerOption) {
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
    if (f) this.setD('removeFile', false);
  }

  docFileLabel(d: DocRow) {
    if (d.file) return `${d.file.name} (new)`;
    return d.existingPath && !d.removeFile ? 'Existing file' : 'No file';
  }

  editDoc(d: DocRow) {
    this.dDraft.set({ ...d });
    this.editingDoc.set(d.uid);
    this.error.set(null);
  }

  cancelDoc(el: HTMLInputElement) {
    this.dDraft.set(newDoc());
    this.editingDoc.set(null);
    el.value = '';
  }

  addDoc(el: HTMLInputElement) {
    const d = this.dDraft();
    if (!d.title.trim()) {
      this.error.set('Document title is required.');
      return;
    }
    const editing = this.editingDoc();
    if (editing !== null) {
      this.docs.update((l) =>
        l.map((x) => (x.uid === editing ? { ...d, title: d.title.trim(), uid: editing } : x)),
      );
      this.editingDoc.set(null);
    } else {
      if (this.docs().length >= 30) {
        this.error.set('Max 30 documents.');
        return;
      }
      this.docs.update((l) => [...l, { ...d, title: d.title.trim(), uid: ++this.uid }]);
    }
    this.dDraft.set(newDoc());
    el.value = '';
    this.error.set(null);
  }

  removeDoc(uid: number) {
    this.docs.update((l) => l.filter((x) => x.uid !== uid));
    if (this.editingDoc() === uid) {
      this.editingDoc.set(null);
      this.dDraft.set(newDoc());
    }
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
    if (f) this.setH('removeFile', false);
  }

  histFileLabel(h: HistRow) {
    if (h.file) return `${h.file.name} (new)`;
    return h.existingPath && !h.removeFile ? 'Existing letter' : '';
  }

  editHist(h: HistRow) {
    this.hDraft.set({ ...h });
    this.editingHist.set(h.uid);
    this.error.set(null);
  }

  cancelHist(el: HTMLInputElement) {
    this.hDraft.set(newHist());
    this.editingHist.set(null);
    el.value = '';
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
    const editing = this.editingHist();
    if (editing !== null) {
      this.hist.update((l) =>
        l.map((x) => (x.uid === editing ? { ...h, employer: h.employer.trim(), uid: editing } : x)),
      );
      this.editingHist.set(null);
    } else {
      if (this.hist().length >= 20) {
        this.error.set('Max 20 employment entries.');
        return;
      }
      this.hist.update((l) => [...l, { ...h, employer: h.employer.trim(), uid: ++this.uid }]);
    }
    this.hDraft.set(newHist());
    el.value = '';
    this.error.set(null);
  }

  removeHist(uid: number) {
    this.hist.update((l) => l.filter((x) => x.uid !== uid));
    if (this.editingHist() === uid) {
      this.editingHist.set(null);
      this.hDraft.set(newHist());
    }
  }

  // ---------- submit ----------
  submit() {
    if (this.saving()) return;

    const f = this.form();
    const fail = (t: Tab, m: string) => {
      this.tab.set(t);
      this.error.set(m);
    };

    const cid = this.companyId();
    if (cid === null) return fail('basic', 'Select a company.');

    if (!f.employeeCode.trim()) return fail('basic', 'Employee ID is required.');
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
    if (f.newPassword) {
      if (f.newPassword.length < 8)
        return fail('security', 'Password must be at least 8 characters.');
      if (!/[A-Za-z]/.test(f.newPassword) || !/\d/.test(f.newPassword))
        return fail('security', 'Password needs a letter and a number.');
      if (f.newPassword !== f.confirmPassword) return fail('security', 'Passwords do not match.');
    }

    const docs = this.docs();
    const hist = this.hist();

    const payload = {
      companyId: cid,
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
      newPassword: f.newPassword || null,
      confirmPassword: f.confirmPassword || null,
      removeProfilePicture: this.removeProfile(),
      documents: docs.map((d, i) => ({
        id: d.id,
        type: d.type,
        title: d.title,
        description: n(d.description),
        fileKey: d.file ? `doc_${i}` : null,
        removeFile: d.removeFile,
      })),
      histories: hist.map((h, i) => ({
        id: h.id,
        employer: h.employer,
        startDate: h.startDate || null,
        endDate: h.endDate || null,
        reasonForLeaving: n(h.reason),
        fileKey: h.file ? `hist_${i}` : null,
        removeFile: h.removeFile,
      })),
      managerUserIds: this.managersLoaded() ? this.managers().map((m) => m.userId) : null,
    };

    const fd = new FormData();
    fd.append('payload', JSON.stringify(payload));
    const pf = this.profile();
    if (pf) fd.append('profilePicture', pf);
    docs.forEach((d, i) => d.file && fd.append(`doc_${i}`, d.file));
    hist.forEach((h, i) => h.file && fd.append(`hist_${i}`, h.file));

    this.saving.set(true);
    this.error.set(null);

    this.http.put(`${this.api}/main/employee-edit/${this.id}`, fd).subscribe({
      next: () => {
        this.saving.set(false);
        this.router.navigate(['/employee/list']);
      },
      error: (e: HttpErrorResponse) => {
        this.saving.set(false);
        this.error.set(this.msg(e, 'Could not update employee.'));
      },
    });
  }
}
