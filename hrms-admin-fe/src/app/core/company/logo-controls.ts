import { Component, model } from '@angular/core';
import { DEFAULT_LOGO, LogoPlace, LogoShape, LogoStyle } from './brand-logo';

@Component({
  selector: 'app-logo-controls',
  template: `
    <div class="space-y-4 rounded-xl border border-slate-200 bg-white/70 p-4">
      <div>
        <p class="mb-2 text-[13px] font-medium text-slate-600">Shape</p>
        <div class="grid grid-cols-5 gap-2">
          @for (s of shapes; track s.key) {
            <button
              type="button"
              (click)="set('shape', s.key)"
              class="rounded-lg border px-2 py-2 text-xs font-medium transition"
              [class]="
                value().shape === s.key
                  ? 'border-blue-500 bg-blue-50 text-blue-700'
                  : 'border-slate-200 bg-white/70 text-slate-600 hover:border-blue-300'
              "
            >
              {{ s.label }}
            </button>
          }
        </div>
        <p class="mt-1.5 text-xs text-slate-500">
          Auto detects wide or square logos. Direct shows the logo with no box.
        </p>
      </div>

      <div>
        <p class="mb-2 text-[13px] font-medium text-slate-600">Show in</p>
        <div class="grid grid-cols-3 gap-2">
          @for (p of places; track p.key) {
            <button
              type="button"
              (click)="set('place', p.key)"
              class="rounded-lg border px-2 py-2 text-xs font-medium transition"
              [class]="
                value().place === p.key
                  ? 'border-blue-500 bg-blue-50 text-blue-700'
                  : 'border-slate-200 bg-white/70 text-slate-600 hover:border-blue-300'
              "
            >
              {{ p.label }}
            </button>
          }
        </div>
      </div>

      <div>
        <p class="mb-2 text-[13px] font-medium text-slate-600">Crop</p>
        <div class="grid grid-cols-2 gap-2">
          <button
            type="button"
            (click)="set('fit', 'contain')"
            class="rounded-lg border px-2 py-2 text-xs font-medium transition"
            [class]="
              value().fit === 'contain'
                ? 'border-blue-500 bg-blue-50 text-blue-700'
                : 'border-slate-200 bg-white/70 text-slate-600'
            "
          >
            Fit whole logo
          </button>
          <button
            type="button"
            (click)="set('fit', 'cover')"
            class="rounded-lg border px-2 py-2 text-xs font-medium transition"
            [class]="
              value().fit === 'cover'
                ? 'border-blue-500 bg-blue-50 text-blue-700'
                : 'border-slate-200 bg-white/70 text-slate-600'
            "
          >
            Fill and crop
          </button>
        </div>
      </div>

      <div class="grid gap-3 sm:grid-cols-2">
        @for (s of sliders; track s.key) {
          @if (value().shape !== 'none' || (s.key !== 'pad' && s.key !== 'border')) {
            <label class="block text-xs font-medium text-slate-600">
              {{ s.label }} <span class="text-slate-400">{{ value()[s.key] }}</span>
              <input
                type="range"
                [min]="s.min"
                [max]="s.max"
                [step]="s.step"
                [value]="value()[s.key]"
                (input)="num(s.key, $event)"
                class="mt-1 w-full accent-blue-600"
              />
            </label>
          }
        }
      </div>

      @if (value().shape !== 'none') {
        <div class="flex flex-wrap items-center gap-2">
          <label
            class="flex items-center gap-2 rounded-lg border border-slate-200 bg-white/70 px-3 py-2 text-xs font-medium text-slate-600"
          >
            Background
            <input
              type="color"
              [value]="value().bg || '#ffffff'"
              (input)="set('bg', $any($event.target).value)"
              class="h-6 w-8 cursor-pointer rounded border-0 bg-transparent p-0"
            />
          </label>
          <button
            type="button"
            (click)="set('bg', '')"
            class="rounded-lg border px-3 py-2 text-xs font-medium transition"
            [class]="
              value().bg === ''
                ? 'border-blue-500 bg-blue-50 text-blue-700'
                : 'border-slate-200 bg-white/70 text-slate-600'
            "
          >
            Transparent
          </button>
          <label
            class="flex items-center gap-2 rounded-lg border border-slate-200 bg-white/70 px-3 py-2 text-xs font-medium text-slate-600"
          >
            Border
            <input
              type="color"
              [value]="value().borderColor"
              (input)="set('borderColor', $any($event.target).value)"
              class="h-6 w-8 cursor-pointer rounded border-0 bg-transparent p-0"
            />
          </label>
        </div>
      }

      <div class="flex items-center justify-between">
        <label class="flex items-center gap-2 text-xs font-medium text-slate-600">
          <input
            type="checkbox"
            [checked]="value().showName"
            (change)="set('showName', $any($event.target).checked)"
          />
          Show company name next to logo
        </label>
        <button
          type="button"
          (click)="reset()"
          class="text-xs font-medium text-blue-600 hover:text-blue-500"
        >
          Reset
        </button>
      </div>
    </div>
  `,
})
export class LogoControls {
  readonly value = model<LogoStyle>({ ...DEFAULT_LOGO });

  shapes: { key: LogoShape; label: string }[] = [
    { key: 'auto', label: 'Auto' },
    { key: 'square', label: 'Square' },
    { key: 'rounded', label: 'Rounded' },
    { key: 'circle', label: 'Circle' },
    { key: 'none', label: 'Direct' },
  ];
  places: { key: LogoPlace; label: string }[] = [
    { key: 'sidebar', label: 'Sidebar' },
    { key: 'navbar', label: 'Navbar' },
    { key: 'both', label: 'Both' },
  ];
  sliders: {
    key: 'zoom' | 'x' | 'y' | 'pad' | 'border';
    label: string;
    min: number;
    max: number;
    step: number;
  }[] = [
    { key: 'zoom', label: 'Zoom', min: 0.5, max: 3, step: 0.05 },
    { key: 'x', label: 'Move left/right', min: -50, max: 50, step: 1 },
    { key: 'y', label: 'Move up/down', min: -50, max: 50, step: 1 },
    { key: 'pad', label: 'Padding', min: 0, max: 12, step: 1 },
    { key: 'border', label: 'Border width', min: 0, max: 4, step: 1 },
  ];

  set(k: keyof LogoStyle, v: unknown) {
    this.value.update((c) => ({ ...c, [k]: v }));
  }
  num(k: keyof LogoStyle, e: Event) {
    this.set(k, +(e.target as HTMLInputElement).value);
  }
  reset() {
    this.value.set({ ...DEFAULT_LOGO });
  }
}
