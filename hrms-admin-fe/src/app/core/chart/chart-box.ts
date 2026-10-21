import { Component, ElementRef, OnDestroy, effect, input, viewChild } from '@angular/core';
import { Chart, ChartConfiguration, registerables } from 'chart.js';

Chart.register(...registerables);

const typeOf = (c: unknown): string | undefined => (c as { type?: string }).type;

@Component({
  selector: 'app-chart',
  template: '<canvas #cv></canvas>',
  host: { class: 'relative block h-full w-full' },
})
export class ChartBox implements OnDestroy {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  config = input.required<ChartConfiguration<any>>();
  private cv = viewChild.required<ElementRef<HTMLCanvasElement>>('cv');
  private chart?: Chart;

  constructor() {
    effect(() => {
      const cfg = this.config();
      const el = this.cv().nativeElement;
      const options = { responsive: true, maintainAspectRatio: false, ...cfg.options };

      // same chart type: just feed the new numbers, so live refreshes animate smoothly
      if (this.chart && typeOf(this.chart.config) === typeOf(cfg)) {
        this.chart.data = cfg.data;
        this.chart.options = options;
        this.chart.update();
        return;
      }

      this.chart?.destroy();
      this.chart = new Chart(el, { ...cfg, options });
    });
  }

  ngOnDestroy() {
    this.chart?.destroy();
  }
}
