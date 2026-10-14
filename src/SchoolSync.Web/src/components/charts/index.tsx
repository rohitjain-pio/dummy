// Lightweight SVG charts. Each is role="img" with a text summary, plus the full data as a
// visually-hidden table — so every figure is screen-reader legible (UI-001 a11y: "text, not canvas-only").
// Colours are token CSS variables only. Empty data renders a flat, labelled empty state rather than a
// broken axis (UI-015 improved-beyond-mockup).

const SERIES = ['var(--color-primary)', 'var(--color-success)', 'var(--color-info)', 'var(--color-warning-text)', 'var(--color-error)', 'var(--color-text-secondary)'];

export interface Datum { label: string; value: number }

function DataTableSr({ title, data, unit }: { title: string; data: Datum[]; unit?: string }) {
  // sr-only must sit on a block wrapper, never on the <table> itself: table layout is auto-width and
  // ignores the 1px box, so the table stays content-width and pushes document scrollWidth past the
  // viewport (a real NFR-025 horizontal-overflow defect, measured at 2px on UI-010 at bp-lg/bp-xl).
  return (
    <div className="sr-only">
      <table>
        <caption>{title}</caption>
        <thead><tr><th scope="col">Label</th><th scope="col">Value</th></tr></thead>
        <tbody>{data.map((d) => <tr key={d.label}><td>{d.label}</td><td>{d.value}{unit}</td></tr>)}</tbody>
      </table>
    </div>
  );
}

function Empty({ title }: { title: string }) {
  return <div className="grid h-40 place-items-center rounded-md border border-dashed border-border text-body-sm text-text-secondary">No data yet for {title.toLowerCase()}</div>;
}

export function BarChart({ title, data, unit = '' }: { title: string; data: Datum[]; unit?: string }) {
  if (data.length === 0) return <Empty title={title} />;
  const max = Math.max(1, ...data.map((d) => d.value));
  const w = 320, h = 160, gap = 8, bw = (w - gap * (data.length - 1)) / data.length;
  return (
    <figure>
      <svg viewBox={`0 0 ${w} ${h + 20}`} className="h-44 w-full" role="img" aria-label={`${title}: ${data.map((d) => `${d.label} ${d.value}${unit}`).join(', ')}`}>
        {data.map((d, i) => {
          const bh = (d.value / max) * h;
          return (
            <g key={d.label}>
              <rect x={i * (bw + gap)} y={h - bh} width={bw} height={bh} rx={4} fill="var(--color-primary)" />
              <text x={i * (bw + gap) + bw / 2} y={h + 14} textAnchor="middle" fontSize="10" fill="var(--color-text-secondary)">{d.label.slice(0, 10)}</text>
            </g>
          );
        })}
      </svg>
      <DataTableSr title={title} data={data} unit={unit} />
    </figure>
  );
}

export function DonutChart({ title, data }: { title: string; data: Datum[] }) {
  const total = data.reduce((s, d) => s + d.value, 0);
  if (total === 0) return <Empty title={title} />;
  const r = 50, c = 2 * Math.PI * r;
  const lengths = data.map((d) => (d.value / total) * c);
  const offsets = lengths.map((_, i) => lengths.slice(0, i).reduce((s, l) => s + l, 0));
  return (
    <figure className="flex flex-wrap items-center gap-6">
      <svg viewBox="0 0 140 140" className="size-36" role="img" aria-label={`${title}: ${data.map((d) => `${d.label} ${d.value}`).join(', ')}`}>
        <g transform="rotate(-90 70 70)">
          {data.map((d, i) => (
            <circle key={d.label} cx="70" cy="70" r={r} fill="none" stroke={SERIES[i % SERIES.length]} strokeWidth="18" strokeDasharray={`${lengths[i]} ${c - lengths[i]!}`} strokeDashoffset={-offsets[i]!} />
          ))}
        </g>
      </svg>
      <ul aria-hidden className="space-y-1 text-body-sm">
        {data.map((d, i) => (
          <li key={d.label} className="flex items-center gap-2">
            <span className="size-2.5 rounded-full" style={{ background: SERIES[i % SERIES.length] }} />
            <span className="text-text-secondary">{d.label}</span><span className="font-semibold text-text-primary">{d.value}</span>
          </li>
        ))}
      </ul>
      <DataTableSr title={title} data={data} />
    </figure>
  );
}

export function AreaChart({ title, series, labels, unit = '%' }: { title: string; series: { name: string; values: (number | undefined)[] }[]; labels: string[]; unit?: string }) {
  const hasData = series.some((s) => s.values.some((v) => v !== undefined));
  if (!hasData) return <Empty title={title} />;
  const w = 320, h = 140, max = 100;
  const x = (i: number) => (labels.length <= 1 ? w / 2 : (i / (labels.length - 1)) * w);
  const y = (v: number) => h - (v / max) * h;
  return (
    <figure>
      <svg viewBox={`0 0 ${w} ${h + 20}`} className="h-44 w-full" role="img"
        aria-label={`${title}: ${series.map((s) => `${s.name} ${s.values.map((v, i) => `${labels[i]} ${v ?? 'no data'}${v !== undefined ? unit : ''}`).join(', ')}`).join('; ')}`}>
        {series.map((s, si) => {
          const pts = s.values.map((v, i) => (v === undefined ? null : `${x(i)},${y(v)}`)).filter(Boolean).join(' ');
          return <polyline key={s.name} points={pts} fill="none" stroke={SERIES[si % SERIES.length]} strokeWidth="2.5" strokeLinejoin="round" />;
        })}
        {labels.map((l, i) => <text key={l + i} x={x(i)} y={h + 14} textAnchor="middle" fontSize="9" fill="var(--color-text-secondary)">{l.slice(5)}</text>)}
      </svg>
      <ul aria-hidden className="mt-2 flex flex-wrap gap-4 text-caption text-text-secondary">
        {series.map((s, i) => <li key={s.name} className="flex items-center gap-1.5"><span className="h-0.5 w-4" style={{ background: SERIES[i % SERIES.length] }} />{s.name}</li>)}
      </ul>
      {series.map((s) => <DataTableSr key={s.name} title={`${title} — ${s.name}`} data={s.values.map((v, i) => ({ label: labels[i]!, value: v ?? 0 }))} unit={unit} />)}
    </figure>
  );
}
