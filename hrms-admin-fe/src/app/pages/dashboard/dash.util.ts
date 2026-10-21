import { ChartConfiguration } from 'chart.js';

export const C = {
  green: '#10b981',
  red: '#ef4444',
  amber: '#f59e0b',
  sky: '#0ea5e9',
  violet: '#8b5cf6',
  teal: '#14b8a6',
  pink: '#ec4899',
  slate: '#94a3b8',
  indigo: '#6366f1',
  lime: '#84cc16',
  orange: '#f97316',
};

export const BAND_COLORS = ['#059669', '#65a30d', '#d97706', '#ea580c', '#dc2626'];
export const BAND_LABELS = ['On time', '1–15 min', '16–30 min', '31–60 min', '60+ min'];

export interface Series {
  label: string;
  data: number[];
  color: string;
}

const grid = 'rgba(148,163,184,0.18)';
const legend = (text: string) => ({ labels: { color: text, boxWidth: 10, font: { size: 11 } } });
const tick = (text: string) => ({ color: text, font: { size: 10 } });

export const shortDate = (iso: string) => {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, d)).toLocaleDateString('en-IN', {
    timeZone: 'UTC',
    day: '2-digit',
    month: 'short',
  });
};

export const donut = (
  labels: string[],
  data: number[],
  colors: string[],
  text: string,
): ChartConfiguration<'doughnut'> => ({
  type: 'doughnut',
  data: { labels, datasets: [{ data, backgroundColor: colors, borderWidth: 0, hoverOffset: 6 }] },
  options: { cutout: '68%', plugins: { legend: { position: 'bottom', ...legend(text) } } },
});

export const stack = (
  labels: string[],
  series: Series[],
  text: string,
  horizontal = false,
  stacked = true,
): ChartConfiguration<'bar'> => ({
  type: 'bar',
  data: {
    labels,
    datasets: series.map((s) => ({
      label: s.label,
      data: s.data,
      backgroundColor: s.color,
      borderRadius: 4,
      maxBarThickness: 28,
    })),
  },
  options: {
    indexAxis: horizontal ? 'y' : 'x',
    plugins: { legend: legend(text) },
    scales: {
      x: { stacked, ticks: tick(text), grid: horizontal ? { color: grid } : { display: false } },
      y: {
        stacked,
        beginAtZero: true,
        ticks: { ...tick(text), precision: 0 },
        grid: horizontal ? { display: false } : { color: grid },
      },
    },
  },
});

// one bar per label, each bar its own colour
export const bars = (
  labels: string[],
  data: number[],
  colors: string[],
  text: string,
): ChartConfiguration<'bar'> => ({
  type: 'bar',
  data: {
    labels,
    datasets: [{ data, backgroundColor: colors, borderRadius: 6, maxBarThickness: 36 }],
  },
  options: {
    plugins: { legend: { display: false } },
    scales: {
      x: { ticks: tick(text), grid: { display: false } },
      y: { beginAtZero: true, ticks: { ...tick(text), precision: 0 }, grid: { color: grid } },
    },
  },
});

export const lines = (
  labels: string[],
  series: Series[],
  text: string,
): ChartConfiguration<'line'> => ({
  type: 'line',
  data: {
    labels,
    datasets: series.map((s) => ({
      label: s.label,
      data: s.data,
      borderColor: s.color,
      backgroundColor: s.color + '33',
      fill: true,
      tension: 0.35,
      pointRadius: 2,
      borderWidth: 2,
    })),
  },
  options: {
    interaction: { mode: 'index', intersect: false },
    plugins: { legend: legend(text) },
    scales: {
      x: { ticks: tick(text), grid: { display: false } },
      y: { beginAtZero: true, ticks: { ...tick(text), precision: 0 }, grid: { color: grid } },
    },
  },
});
