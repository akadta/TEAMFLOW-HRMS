import { Component, inject } from '@angular/core';
import { dt12 } from '../../pages/my-companies/mc.util';
import { NotifyService } from './notify.service';

@Component({
  selector: 'app-notify-modal',
  template: `
    @if (n.show() && n.list().length) {
      <div
        class="fixed inset-0 z-[90] flex items-center justify-center bg-slate-900/60 p-3 backdrop-blur-sm"
        (click)="n.hide()"
      >
        <div
          class="flex max-h-[85vh] w-full max-w-lg flex-col rounded-xl bg-white shadow-2xl"
          (click)="$event.stopPropagation()"
        >
          <div class="flex items-center gap-2 border-b border-slate-200 px-4 py-3">
            <span class="h-2.5 w-2.5 animate-pulse rounded-full bg-amber-400"></span>
            <h3 class="text-sm font-semibold text-slate-900">
              Notifications ({{ n.list().length }})
            </h3>
            <button
              type="button"
              class="ml-auto rounded-md bg-slate-100 px-2.5 py-1 text-[11px] font-semibold text-slate-600 hover:bg-slate-200"
              (click)="n.dismissAll()"
            >
              Dismiss all
            </button>
            <button
              type="button"
              class="rounded-md bg-slate-100 px-2.5 py-1 text-[11px] font-semibold text-slate-600 hover:bg-slate-200"
              (click)="n.hide()"
            >
              Close
            </button>
          </div>

          <div class="min-h-0 flex-1 space-y-2 overflow-y-auto p-3">
            @for (c of n.list(); track c.id) {
              <div class="rounded-lg bg-slate-50 p-3">
                <div class="flex items-start gap-2">
                  <span class="mt-0.5 text-sm">{{ icon(c.kind) }}</span>
                  <div class="min-w-0 flex-1">
                    <p class="text-xs font-semibold text-slate-900">{{ c.title }}</p>
                    <p class="mt-0.5 whitespace-pre-wrap text-xs text-slate-700">{{ c.text }}</p>
                    <p class="mt-1 text-[11px] text-slate-400">{{ dt12(c.createdAt) }}</p>
                  </div>
                </div>
                <div class="mt-2 flex justify-end gap-2">
                  <button
                    type="button"
                    class="rounded-md bg-slate-200 px-3 py-1 text-[11px] font-semibold text-slate-700 hover:bg-slate-300"
                    (click)="n.dismiss(c)"
                  >
                    Dismiss
                  </button>
                  @if (c.link) {
                    <button
                      type="button"
                      class="rounded-md bg-blue-600 px-3 py-1 text-[11px] font-semibold text-white hover:bg-blue-500"
                      (click)="n.dismiss(c, true)"
                    >
                      Open
                    </button>
                  }
                </div>
              </div>
            }
          </div>
        </div>
      </div>
    }

    @if (!n.show() && n.list().length) {
      <button
        type="button"
        class="fixed bottom-4 right-4 z-[80] flex items-center gap-2 rounded-full bg-blue-600 px-4 py-2 text-xs font-semibold text-white shadow-xl hover:bg-blue-500"
        (click)="n.reopen()"
      >
        <span class="h-2 w-2 animate-pulse rounded-full bg-amber-300"></span
        >{{ n.list().length }} notification(s)
      </button>
    }
  `,
})
export class NotifyModal {
  readonly n = inject(NotifyService);
  readonly dt12 = dt12;

  icon(kind: string) {
    return kind === 'approval' ? '✅' : kind === 'task' ? '📝' : 'ℹ️';
  }
}
