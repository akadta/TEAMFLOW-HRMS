import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { ManagerOption, ManagerPicker } from '../../../shared/manager-picker/manager-picker';

interface RoleOption {
  id: number;
  name: string;
  isActive: boolean;
}
interface Detail {
  id: number;
  userId: number;
  roleId: number;
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
  workType: string | null;
}
type Tab = 'basic' | 'personal' | 'professional' | 'designation' | 'security';

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
  roleId: '',
  workType: '',
  password: '',
  confirmPassword: '',
});
type Form = ReturnType<typeof emptyForm>;

const n = (v: string) => {
  const t = v.trim();
  return t ? t : null;
};
const s = (v: string | null | undefined) => v ?? '';
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const MB = 1024 * 1024;

@Component({
  selector: 'app-main-employee-form',
  imports: [RouterLink, ManagerPicker],
  templateUrl: './main-employee-form.html',
  styleUrl: './main-employee-form.css',
})
export class MainEmployeeForm implements OnDestroy {
  private http = inject(HttpClient);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private api = environment.apiUrl;
  private host = environment.apiUrl.replace(/\/api\/?$/, '');
  private readonly id = Number(this.route.snapshot.paramMap.get('id') ?? 0);

  readonly isEdit = this.id > 0;
  readonly tabs: { id: Tab; label: string }[] = [
    { id: 'basic', label: 'Basic Info' },
    { id: 'personal', label: 'Personal' },
    { id: 'professional', label: 'Professional' },
    { id: 'designation', label: 'Designation' },
    { id: 'security', label: 'Password' },
  ];
  readonly genders = ['Male', 'Female', 'Other'];
  readonly bloodGroups = ['A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-'];
  readonly maritals = ['Single', 'Married', 'Divorced', 'Widowed'];
  readonly workTypes = [
    { id: 'office', label: 'In Office' },
    { id: 'remote', label: 'Work From Home' },
    { id: 'hybrid', label: 'Hybrid' },
  ];

  roles = signal<RoleOption[]>([]);
  form = signal<Form>(emptyForm());
  roleOptions = computed(() =>
    this.roles().filter((r) => r.isActive || '' + r.id === this.form().roleId),
  );
  tab = signal<Tab>('basic');

  existingProfile = signal<string | null>(null);
  removeProfile = signal(false);
  profile = signal<File | null>(null);
  preview = signal<string | null>(null);

  managers = signal<ManagerOption[]>([]);
  managerIds = computed(() => this.managers().map((m) => m.userId));
  pickerOpen = signal(false);
  pickerMsg = signal<string | null>(null);
  selfUserId = signal<number | null>(null);
  managersLoaded = signal(false); // never wipe managers if they failed to load

  loading = signal(this.isEdit);
  loadError = signal<string | null>(null);
  saving = signal(false);
  showPw = signal(false);
  error = signal<string | null>(null);
  success = signal<string | null>(null);

  constructor() {
    this.http.get<RoleOption[]>(`${this.api}/main/roles`).subscribe({
      next: (l) => this.roles.set(l),
      error: () => this.error.set('Could not load roles.'),
    });

    if (!this.isEdit) {
      this.autoCode();
      return;
    }

    this.http.get<Detail>(`${this.api}/main/main-employees/${this.id}`).subscribe({
      next: (d) => {
        this.fill(d);
        this.selfUserId.set(d.userId);
        this.loading.set(false);
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
      },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.loadError.set(this.msg(e, 'Could not load main employee.'));
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

  autoCode() {
    this.http.get<{ code: string }>(`${this.api}/main/main-employees/next-code`).subscribe({
      next: (r) => this.set('employeeCode', r.code),
      error: (e: HttpErrorResponse) =>
        this.error.set(this.msg(e, 'Could not generate employee ID.')),
    });
  }

  private fill(d: Detail) {
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
      roleId: d.roleId.toString(),
      workType: s(d.workType),
      password: '',
      confirmPassword: '',
    });
    this.existingProfile.set(d.profilePicture);
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

  // ---------- reporting managers (main team only) ----------
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

  // ---------- submit ----------
  private reset() {
    this.form.set(emptyForm());
    this.setProfile(null);
    this.managers.set([]);
    this.showPw.set(false);
    this.tab.set('basic');
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
    if (!f.roleId) return fail('designation', 'Select a role.');

    if (!this.isEdit || f.password) {
      if (f.password.length < 8) return fail('security', 'Password must be at least 8 characters.');
      if (!/[A-Za-z]/.test(f.password) || !/\d/.test(f.password))
        return fail('security', 'Password needs a letter and a number.');
      if (f.password !== f.confirmPassword) return fail('security', 'Passwords do not match.');
    }

    const payload = {
      roleId: Number(f.roleId),
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
      workType: n(f.workType),
      password: f.password || null,
      confirmPassword: f.confirmPassword || null,
      removeProfilePicture: this.removeProfile(),
      managerUserIds:
        !this.isEdit || this.managersLoaded() ? this.managers().map((m) => m.userId) : null,
    };

    const fd = new FormData();
    fd.append('payload', JSON.stringify(payload));
    const pf = this.profile();
    if (pf) fd.append('profilePicture', pf);

    this.saving.set(true);
    this.error.set(null);
    this.success.set(null);

    const url = `${this.api}/main/main-employees`;
    const call$ = this.isEdit
      ? this.http.put<{ employeeCode: string }>(`${url}/${this.id}`, fd)
      : this.http.post<{ employeeCode: string }>(url, fd);

    call$.subscribe({
      next: (r) => {
        this.saving.set(false);
        if (this.isEdit) {
          this.router.navigate(['/main-employee/list']);
        } else {
          this.success.set(`Main employee registered. ID: ${r.employeeCode}`);
          this.reset();
          this.autoCode();
        }
      },
      error: (e: HttpErrorResponse) => {
        this.saving.set(false);
        this.error.set(this.msg(e, 'Could not save main employee.'));
      },
    });
  }
}
