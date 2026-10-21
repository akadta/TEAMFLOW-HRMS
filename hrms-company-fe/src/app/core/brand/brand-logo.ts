import { Component, computed, input, signal } from '@angular/core';

export type LogoShape = 'auto' | 'square' | 'rounded' | 'circle' | 'none';
export type LogoPlace = 'sidebar' | 'navbar' | 'both';

export interface LogoStyle {
  shape: LogoShape;
  place: LogoPlace;
  bg: string; // '' = transparent
  fit: 'contain' | 'cover';
  zoom: number;
  x: number; // move, in %
  y: number;
  pad: number;
  border: number;
  borderColor: string;
  showName: boolean;
}

export const DEFAULT_LOGO: LogoStyle = {
  shape: 'rounded',
  place: 'sidebar',
  bg: '#ffffff',
  fit: 'contain',
  zoom: 1,
  x: 0,
  y: 0,
  pad: 2,
  border: 0,
  borderColor: '#ffffff',
  showName: true,
};

export function parseLogo(l?: Partial<LogoStyle> | null): LogoStyle {
  return { ...DEFAULT_LOGO, ...(l ?? {}) };
}

@Component({
  selector: 'app-brand-logo',
  template: `
    @if (src(); as s) {
      <div class="shrink-0 overflow-hidden" [style]="box()">
        <img
          [src]="s"
          alt=""
          draggable="false"
          (load)="onLoad($event)"
          class="block h-full w-full"
          [style.object-fit]="logo().fit"
          [style.transform]="
            'translate(' + logo().x + '%, ' + logo().y + '%) scale(' + logo().zoom + ')'
          "
        />
      </div>
    } @else {
      <div
        class="grid shrink-0 place-items-center font-bold"
        [style.width.px]="size()"
        [style.height.px]="size()"
        [style.border-radius.px]="logo().shape === 'circle' ? size() : size() * 0.25"
        [style.background]="accent()"
        [style.color]="accentText()"
        [style.font-size.px]="size() * 0.4"
      >
        {{ initial() }}
      </div>
    }
  `,
})
export class BrandLogo {
  readonly src = input<string | null>(null);
  readonly logo = input<LogoStyle>(DEFAULT_LOGO);
  readonly size = input(36);
  readonly compact = input(false); // true = always square (collapsed sidebar)
  readonly initial = input('H');
  readonly accent = input('#2563eb');
  readonly accentText = input('#ffffff');

  private ratio = signal(1);

  private factor = computed(() => {
    const s = this.logo().shape;
    if (this.compact() || !(s === 'auto' || s === 'none')) return 1;
    return Math.min(Math.max(this.ratio(), 1), 3.2);
  });

  private radius = computed(() => {
    const s = this.logo().shape;
    if (s === 'none' || s === 'square') return 0;
    if (s === 'circle') return this.size();
    return Math.round(this.size() * 0.25);
  });

  readonly box = computed(() => {
    const l = this.logo();
    const bare = l.shape === 'none';
    return {
      width: this.size() * this.factor() + 'px',
      height: this.size() + 'px',
      padding: (bare ? 0 : l.pad) + 'px',
      background: bare ? 'transparent' : l.bg || 'transparent',
      border: bare || !l.border ? 'none' : `${l.border}px solid ${l.borderColor}`,
      'border-radius': this.radius() + 'px',
      'box-sizing': 'border-box',
    };
  });

  onLoad(e: Event) {
    const i = e.target as HTMLImageElement;
    if (i.naturalWidth && i.naturalHeight) this.ratio.set(i.naturalWidth / i.naturalHeight);
  }
}
