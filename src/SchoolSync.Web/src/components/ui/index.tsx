import * as Dialog from '@radix-ui/react-dialog';
import { Loader2, X } from 'lucide-react';
import { forwardRef, useId, type ButtonHTMLAttributes, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react';
import { cn } from '@/lib/cn';
import type { AttendanceOutcome } from '@/lib/types';

// design-tokens.md §8 component inventory, every visual value a token utility (§7 states).

type Variant = 'primary' | 'secondary' | 'outline' | 'ghost' | 'danger';
const VARIANT: Record<Variant, string> = {
  primary: 'bg-primary text-white hover:bg-primary-hover',
  secondary: 'bg-primary-subtle-bg text-primary-subtle-text hover:bg-primary-subtle-bg-strong',
  outline: 'border border-border bg-surface text-text-primary hover:bg-background',
  ghost: 'text-text-secondary hover:bg-background hover:text-text-primary',
  danger: 'bg-error text-white hover:opacity-90',
};

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  size?: 'sm' | 'md' | 'lg' | 'icon';
  loading?: boolean;
}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  { variant = 'primary', size = 'md', loading, disabled, className, children, type = 'button', ...rest }, ref,
) {
  return (
    <button
      ref={ref}
      type={type}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      className={cn(
        'inline-flex items-center justify-center gap-2 rounded-md font-semibold transition-colors',
        'disabled:pointer-events-none disabled:bg-text-quaternary disabled:text-text-tertiary disabled:border-transparent',
        size === 'sm' && 'h-8 px-3 text-body-sm',
        size === 'md' && 'h-10 px-4 text-body-sm',
        size === 'lg' && 'h-12 px-6 text-body',
        size === 'icon' && 'h-10 w-10 text-body-sm',
        VARIANT[variant],
        className,
      )}
      {...rest}
    >
      {loading && <Loader2 aria-hidden className="size-4 animate-spin" />}
      {children}
    </button>
  );
});

export function Card({ className, children, as: As = 'div' }: { className?: string; children: ReactNode; as?: 'div' | 'section' | 'article' | 'li' }) {
  return <As className={cn('rounded-lg border border-border bg-surface shadow-card', className)}>{children}</As>;
}

export function PageHeader({ title, description, actions }: { title: string; description?: string; actions?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
      <div>
        <h2 className="text-h2 text-text-primary">{title}</h2>
        {description && <p className="mt-1 text-body-sm text-text-secondary">{description}</p>}
      </div>
      {actions && <div className="flex flex-wrap gap-2">{actions}</div>}
    </div>
  );
}

// ── Form fields: every field has an associated <label>; required → aria-required; error → aria-describedby
interface FieldShellProps { label: string; required?: boolean; error?: string; hint?: string; children: (ids: { id: string; describedBy?: string }) => ReactNode; className?: string }
export function FieldShell({ label, required, error, hint, children, className }: FieldShellProps) {
  const id = useId();
  const errId = `${id}-err`;
  const hintId = `${id}-hint`;
  const describedBy = [error ? errId : null, hint ? hintId : null].filter(Boolean).join(' ') || undefined;
  return (
    <div className={className}>
      <label htmlFor={id} className="text-body-sm font-semibold text-text-primary">
        {label}{required && <span aria-hidden className="text-error"> *</span>}
      </label>
      <div className="mt-1.5">{children({ id, describedBy })}</div>
      {hint && !error && <p id={hintId} className="mt-1 text-caption text-text-secondary">{hint}</p>}
      <p id={errId} aria-live="polite" className="mt-1 text-caption text-error">{error ?? ''}</p>
    </div>
  );
}

const inputCls = 'w-full rounded-md border border-border bg-surface px-3.5 py-2.5 text-body-sm text-text-primary placeholder:text-text-secondary outline-none focus:border-primary focus:ring-2 focus:ring-primary-subtle-bg-strong aria-[invalid=true]:border-error';

type InputProps = InputHTMLAttributes<HTMLInputElement> & { label: string; error?: string; hint?: string };
export const TextField = forwardRef<HTMLInputElement, InputProps>(function TextField({ label, error, hint, required, className, ...rest }, ref) {
  return (
    <FieldShell label={label} required={required} error={error} hint={hint} className={className}>
      {({ id, describedBy }) => (
        <input ref={ref} id={id} required={required} aria-required={required || undefined} aria-invalid={!!error || undefined} aria-describedby={describedBy} className={inputCls} {...rest} />
      )}
    </FieldShell>
  );
});

type SelectProps = SelectHTMLAttributes<HTMLSelectElement> & { label: string; error?: string; children: ReactNode };
export function SelectField({ label, error, required, className, children, ...rest }: SelectProps) {
  return (
    <FieldShell label={label} required={required} error={error} className={className}>
      {({ id, describedBy }) => (
        <select id={id} required={required} aria-required={required || undefined} aria-invalid={!!error || undefined} aria-describedby={describedBy} className={inputCls} {...rest}>
          {children}
        </select>
      )}
    </FieldShell>
  );
}

type TextareaProps = TextareaHTMLAttributes<HTMLTextAreaElement> & { label: string; error?: string; hint?: string };
export function TextareaField({ label, error, hint, required, className, ...rest }: TextareaProps) {
  return (
    <FieldShell label={label} required={required} error={error} hint={hint} className={className}>
      {({ id, describedBy }) => (
        <textarea id={id} required={required} aria-required={required || undefined} aria-invalid={!!error || undefined} aria-describedby={describedBy} className={inputCls} {...rest} />
      )}
    </FieldShell>
  );
}

export function ErrorBanner({ children }: { children: ReactNode }) {
  if (!children) return null;
  return <div role="alert" className="rounded-md border border-error bg-error-subtle-bg px-4 py-3 text-body-sm text-text-primary">{children}</div>;
}

// ── Modal (Radix Dialog: focus trap, Escape closes, labelled by title) — the mockup's modalHost pattern
export function Modal({ open, onOpenChange, title, description, children, size = 'md' }: {
  open: boolean; onOpenChange: (open: boolean) => void; title: string; description?: string; children: ReactNode; size?: 'sm' | 'md' | 'lg';
}) {
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-50 bg-text-primary/50" />
        <Dialog.Content
          className={cn(
            'fixed left-1/2 top-1/2 z-50 max-h-[90vh] w-[calc(100%-2rem)] -translate-x-1/2 -translate-y-1/2 overflow-y-auto rounded-lg bg-surface shadow-card',
            size === 'sm' && 'max-w-sm', size === 'md' && 'max-w-md', size === 'lg' && 'max-w-2xl',
          )}
        >
          <div className="flex items-start justify-between gap-4 border-b border-border-subtle px-6 py-4">
            <div>
              <Dialog.Title className="text-body font-bold text-text-primary">{title}</Dialog.Title>
              {description ? <Dialog.Description className="mt-0.5 text-body-sm text-text-secondary">{description}</Dialog.Description> : <Dialog.Description className="sr-only">{title}</Dialog.Description>}
            </div>
            <Dialog.Close asChild>
              <Button variant="ghost" size="icon" aria-label="Close"><X aria-hidden className="size-5" /></Button>
            </Dialog.Close>
          </div>
          <div className="px-6 py-5">{children}</div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

/** _shared.md: destructive-action confirmation naming what is deleted, "This action cannot be undone." */
export function ConfirmDialog({ open, onOpenChange, subject, actionLabel = 'Delete', onConfirm, loading }: {
  open: boolean; onOpenChange: (o: boolean) => void; subject: string; actionLabel?: string; onConfirm: () => void; loading?: boolean;
}) {
  return (
    <Modal open={open} onOpenChange={onOpenChange} title={`${actionLabel} ${subject}?`} size="sm">
      <p className="text-body-sm text-text-secondary">
        <strong className="text-text-primary">{subject}</strong> will be {actionLabel === 'Delete' ? 'deleted' : actionLabel.toLowerCase() + 'n'}. This action cannot be undone.
      </p>
      <div className="mt-6 flex justify-end gap-2">
        <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
        <Button variant="danger" loading={loading} onClick={onConfirm}>{actionLabel}</Button>
      </div>
    </Modal>
  );
}

// ── Status pills. design-tokens §1a/§1b: ALL FOUR use the light-bg/dark-text pattern. The solid-600
// + white-text pattern is forbidden on text-bearing elements (§1b binding instruction) — it failed
// AA for Present (3.77:1) and Leave (4.10:1), which the old §1b had wrongly cleared (U34).
const PILL: Record<AttendanceOutcome, string> = {
  Present: 'bg-success-subtle-bg text-success-subtle-text',
  Absent: 'bg-error-subtle-bg text-error-subtle-text',
  Late: 'bg-warning-bg text-warning-text',
  Leave: 'bg-info-subtle-bg text-info-subtle-text',
};
export function StatusPill({ outcome }: { outcome: AttendanceOutcome }) {
  return <span className={cn('inline-flex rounded-full px-2.5 py-0.5 text-caption font-semibold', PILL[outcome])}>{outcome}</span>;
}

export function Badge({ children, tone = 'primary' }: { children: ReactNode; tone?: 'primary' | 'success' | 'warning' | 'neutral' }) {
  return (
    <span className={cn('inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-caption font-semibold',
      tone === 'primary' && 'bg-primary-subtle-bg text-primary-subtle-text',
      tone === 'success' && 'bg-success-subtle-bg text-success-subtle-text',
      tone === 'warning' && 'bg-warning-bg text-warning-text',
      tone === 'neutral' && 'bg-border-subtle text-neutral-subtle-text')}>
      {children}
    </span>
  );
}

export function Skeleton({ className }: { className?: string }) {
  return <div aria-hidden className={cn('animate-pulse rounded-md bg-border-subtle', className)} />;
}

/** NFR-004 designed loading state: skeletons, with one polite status for assistive tech. */
export function LoadingState({ label = 'Loading', rows = 3, variant = 'rows' }: { label?: string; rows?: number; variant?: 'rows' | 'cards' | 'tiles' }) {
  return (
    <div role="status" aria-live="polite">
      <span className="sr-only">{label}…</span>
      {variant === 'rows' && <div className="space-y-2">{Array.from({ length: rows }, (_, i) => <Skeleton key={i} className="h-12 w-full" />)}</div>}
      {variant === 'cards' && <div className="grid gap-6 sm:grid-cols-2 xl:grid-cols-3">{Array.from({ length: rows }, (_, i) => <Skeleton key={i} className="h-44 rounded-lg" />)}</div>}
      {variant === 'tiles' && <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">{Array.from({ length: rows }, (_, i) => <Skeleton key={i} className="h-24 rounded-lg" />)}</div>}
    </div>
  );
}

export function EmptyState({ title, description, action }: { title: string; description?: string; action?: ReactNode }) {
  return (
    <Card className="px-6 py-12 text-center">
      <p className="text-body font-semibold text-text-primary">{title}</p>
      {description && <p className="mt-1 text-body-sm text-text-secondary">{description}</p>}
      {action && <div className="mt-4 flex justify-center">{action}</div>}
    </Card>
  );
}

export function Avatar({ name, className }: { name: string; className?: string }) {
  const initials = name.split(/\s+/).filter(Boolean).slice(0, 2).map((p) => p[0]!.toUpperCase()).join('') || '?';
  return <span aria-hidden className={cn('grid size-8 shrink-0 place-items-center rounded-full bg-primary text-caption font-bold text-white', className)}>{initials}</span>;
}

export function StatTile({ label, value, hint }: { label: string; value: ReactNode; hint?: string }) {
  return (
    <Card className="px-4 py-4">
      <p className="text-micro-label uppercase text-text-secondary">{label}</p>
      <p className="mt-2 text-h2 text-text-primary">{value}</p>
      {hint && <p className="mt-1 text-caption text-text-secondary">{hint}</p>}
    </Card>
  );
}

export interface Column<T> { header: string; cell: (row: T) => ReactNode; className?: string }
export function DataTable<T>({ caption, columns, rows, rowKey }: { caption: string; columns: Column<T>[]; rows: T[]; rowKey: (row: T) => string }) {
  // tabIndex+role=region: a horizontally scrollable region must be reachable by keyboard
  // (axe scrollable-region-focusable, WCAG 2.1 AA). NFR-025: the table scrolls here, never the page.
  return (
    <div tabIndex={0} role="region" aria-label={caption} className="overflow-x-auto rounded-lg border border-border bg-surface shadow-card focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary">
      <table className="w-full min-w-[640px] text-body-sm">
        <caption className="sr-only">{caption}</caption>
        <thead className="border-b border-border bg-background text-left text-text-secondary">
          <tr>{columns.map((c) => <th key={c.header} scope="col" className={cn('px-4 py-3 font-semibold', c.className)}>{c.header}</th>)}</tr>
        </thead>
        <tbody className="divide-y divide-border-subtle">
          {rows.map((r) => <tr key={rowKey(r)}>{columns.map((c) => <td key={c.header} className={cn('px-4 py-3 text-text-primary', c.className)}>{c.cell(r)}</td>)}</tr>)}
        </tbody>
      </table>
    </div>
  );
}
