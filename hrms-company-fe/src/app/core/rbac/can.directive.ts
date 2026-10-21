import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';
import { PermissionService } from './permission.service';

@Directive({ selector: '[can]' })
export class CanDirective {
  readonly can = input.required<string | string[]>();

  private tpl = inject(TemplateRef<unknown>);
  private vcr = inject(ViewContainerRef);
  private perm = inject(PermissionService);
  private shown = false;

  constructor() {
    effect(() => {
      const ok = this.perm.can(this.can());
      if (ok && !this.shown) {
        this.vcr.createEmbeddedView(this.tpl);
        this.shown = true;
      } else if (!ok && this.shown) {
        this.vcr.clear();
        this.shown = false;
      }
    });
  }
}
