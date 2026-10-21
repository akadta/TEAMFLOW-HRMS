import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/auth/auth.service';
import { BrandService } from '../../core/brand/brand.service';
import { LeaveRequest } from '../../leave/leave-request/leave-request';
import {
  TodayStatus,
  band,
  dayLabel,
  errMsg,
  hhmm,
  istDate,
  lateText,
  t12,
} from '../attendance.util';

@Component({
  selector: 'app-attendance-card',
  imports: [LeaveRequest],
  templateUrl: './attendance-card.html',
  styleUrl: '../attendance.css',
})
export class AttendanceCard {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/company/attendance`;
  private auth = inject(AuthService);
  private last = 0;
  private offset = 0;
  private stream: MediaStream | null = null;
  private shotBlob: Blob | null = null;
  private vid = viewChild<ElementRef<HTMLVideoElement>>('vid');
  readonly brand = inject(BrandService);

  readonly hhmm = hhmm;
  readonly t12 = t12;
  readonly band = band;
  readonly lateText = lateText;

  hidden = signal(false);
  isEmp = computed(() => !!this.auth.user() && !this.hidden());
  st = signal<TodayStatus | null>(null);
  now = signal(Date.now());
  loading = signal(true);
  busy = signal(false);
  error = signal<string | null>(null);
  modal = signal(false);
  reason = signal('');
  files = signal<File[]>([]);

  cam = signal(false);
  shot = signal<string | null>(null);
  camErr = signal<string | null>(null);
  permMsg = signal<string | null>(null);

  clock = computed(() => t12(new Date(this.now() + this.offset).toISOString()));
  today = computed(() => dayLabel(istDate(this.now() + this.offset)));

  constructor() {
    if (this.isEmp()) this.load();
    const id = setInterval(() => this.tick(), 1000);
    inject(DestroyRef).onDestroy(() => {
      clearInterval(id);
      this.stopStream();
    });
  }

  private apply(s: TodayStatus) {
    this.st.set(s);
    this.offset = Date.parse(s.serverNow) - Date.now();
    void this.checkPerms(s);
  }

  // warn before the click when the browser already blocks location / camera
  private async checkPerms(s: TodayStatus) {
    const ask = async (name: string) => {
      try {
        return (await navigator.permissions.query({ name: name as PermissionName })).state;
      } catch {
        return 'unknown';
      }
    };
    const m: string[] = [];
    if (s.requireLocation && (await ask('geolocation')) === 'denied')
      m.push(
        'Location is blocked. Enable location for this site in your browser settings to mark attendance.',
      );
    if (s.requirePhoto && (await ask('camera')) === 'denied')
      m.push(
        'Camera is blocked. Enable the camera for this site in your browser settings to mark attendance.',
      );
    this.permMsg.set(m.length ? m.join(' ') : null);
  }

  load() {
    this.last = Date.now();
    this.http.get<TodayStatus>(`${this.api}/today`).subscribe({
      next: (s) => {
        this.apply(s);
        this.loading.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        if (e.status === 403) return this.hidden.set(true);
        this.error.set(errMsg(e, 'Could not load attendance.'));
      },
    });
  }

  private tick() {
    this.now.set(Date.now());
    const s = this.st();
    if (!s || Date.now() - this.last < 3000) return;
    const t = Date.now() + this.offset;
    if (istDate(t) !== s.date || (s.nextChangeAt && t >= Date.parse(s.nextChangeAt))) this.load();
  }

  private run(req: Observable<TodayStatus>) {
    this.busy.set(true);
    this.error.set(null);
    req.subscribe({
      next: (s) => {
        this.busy.set(false);
        this.apply(s);
        this.modal.set(false);
        this.closeCam();
        this.reason.set('');
        this.files.set([]);
      },
      error: (e: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(errMsg(e, 'Something went wrong.'));
        this.load();
      },
    });
  }

  private getPos(): Promise<{ lat: number; lng: number; accuracy: number }> {
    return new Promise((res, rej) => {
      if (!navigator.geolocation) return rej(new Error('This device does not support location.'));
      navigator.geolocation.getCurrentPosition(
        (p) =>
          res({ lat: p.coords.latitude, lng: p.coords.longitude, accuracy: p.coords.accuracy }),
        (e) =>
          rej(
            new Error(
              e.code === 1
                ? 'Location is blocked. Allow location for this site (browser address bar), then try again.'
                : 'Could not get your location. Turn on GPS / location and try again.',
            ),
          ),
        { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 },
      );
    });
  }

  // ---------- mark / check-in ----------
  async mark() {
    if (this.busy() || !this.st()) return;
    this.error.set(null);
    if (this.st()!.requirePhoto) return void this.openCam();
    await this.sendMark(null);
  }

  private async sendMark(photo: Blob | null) {
    const s = this.st()!;
    const fd = new FormData();
    this.busy.set(true);
    this.error.set(null);

    try {
      if (s.requireLocation) {
        const p = await this.getPos();
        fd.append('lat', String(p.lat));
        fd.append('lng', String(p.lng));
        fd.append('accuracy', String(p.accuracy));
      }
    } catch (e) {
      this.busy.set(false);
      this.error.set(e instanceof Error ? e.message : 'Could not get your location.');
      return;
    }

    if (photo) fd.append('photo', photo, 'photo.jpg');
    this.run(this.http.post<TodayStatus>(`${this.api}/mark`, fd));
  }

  async checkIn() {
    if (this.busy()) return;
    const fd = new FormData();
    this.busy.set(true);
    this.error.set(null);

    try {
      if (this.st()?.requireLocation) {
        const p = await this.getPos();
        fd.append('lat', String(p.lat));
        fd.append('lng', String(p.lng));
        fd.append('accuracy', String(p.accuracy));
      }
    } catch (e) {
      this.busy.set(false);
      this.error.set(e instanceof Error ? e.message : 'Could not get your location.');
      return;
    }

    this.run(this.http.post<TodayStatus>(`${this.api}/checkin`, fd));
  }

  // ---------- camera ----------
  async openCam() {
    this.cam.set(true);
    this.shot.set(null);
    this.shotBlob = null;
    this.camErr.set(null);
    await new Promise((r) => setTimeout(r, 60));

    try {
      this.stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'user' },
        audio: false,
      });
      const v = this.vid()?.nativeElement;
      if (v) {
        v.srcObject = this.stream;
        await v.play();
      }
    } catch {
      this.camErr.set(
        'Camera is required to mark attendance. Allow camera access for this site and try again.',
      );
    }
  }

  snap() {
    const v = this.vid()?.nativeElement;
    if (!v || !v.videoWidth) return;

    const c = document.createElement('canvas');
    c.width = Math.min(640, v.videoWidth);
    c.height = Math.round((v.videoHeight * c.width) / v.videoWidth);
    c.getContext('2d')!.drawImage(v, 0, 0, c.width, c.height);

    c.toBlob(
      (b) => {
        if (!b) return;
        this.shotBlob = b;
        this.shot.set(URL.createObjectURL(b));
        this.stopStream();
      },
      'image/jpeg',
      0.8,
    );
  }

  retake() {
    const old = this.shot();
    if (old) URL.revokeObjectURL(old);
    void this.openCam();
  }

  sendPhoto() {
    if (this.shotBlob) void this.sendMark(this.shotBlob);
  }

  private stopStream() {
    this.stream?.getTracks().forEach((t) => t.stop());
    this.stream = null;
  }

  closeCam() {
    this.stopStream();
    const old = this.shot();
    if (old) URL.revokeObjectURL(old);
    this.shot.set(null);
    this.shotBlob = null;
    this.cam.set(false);
  }

  // ---------- attendance request ----------
  openModal() {
    this.error.set(null);
    this.modal.set(true);
  }

  pick(e: Event) {
    const el = e.target as HTMLInputElement;
    const add = Array.from(el.files ?? []);
    el.value = '';
    const all = [...this.files(), ...add];
    if (all.length > 5) return this.error.set('Max 5 files.');
    if (add.some((f) => f.size > 8 * 1024 * 1024)) return this.error.set('Each file: max 8 MB.');
    this.error.set(null);
    this.files.set(all);
  }

  drop(i: number) {
    this.files.update((l) => l.filter((_, x) => x !== i));
  }

  send() {
    const r = this.reason().trim();
    if (r.length < 5) return this.error.set('Write a reason (min 5 characters).');
    const fd = new FormData();
    fd.append('reason', r);
    this.files().forEach((f) => fd.append('files', f));
    this.run(this.http.post<TodayStatus>(`${this.api}/request`, fd));
  }
}
