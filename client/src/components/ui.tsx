import type { ReactNode, InputHTMLAttributes, SelectHTMLAttributes, TextareaHTMLAttributes } from 'react';
import { createContext, useCallback, useContext, useEffect, useState } from 'react';

/* ===========================================================================
   TAMS component library (08 §6.2). Accessible by construction, consuming the
   semantic design tokens in index.css. Solve each concern once, reuse everywhere.
   =========================================================================== */

/* --------------------------------------------------------------------------
   Status pill — always text label + icon, never colour alone (08 §12, WCAG).
   Signature preserved for existing callers/tests.
   -------------------------------------------------------------------------- */
type Tone = 'success' | 'warning' | 'danger' | 'info' | 'neutral';

const TONES: Record<Tone, string> = {
  success: 'text-[var(--color-present)] bg-[var(--color-present-bg)] ring-[var(--color-present)]/15',
  warning: 'text-[var(--color-late)] bg-[var(--color-late-bg)] ring-[var(--color-late)]/15',
  danger: 'text-[var(--color-absent)] bg-[var(--color-absent-bg)] ring-[var(--color-absent)]/15',
  info: 'text-[var(--color-leave)] bg-[var(--color-leave-bg)] ring-[var(--color-leave)]/15',
  neutral: 'text-[var(--color-neutral-pill)] bg-[var(--color-neutral-bg)] ring-[var(--color-neutral-pill)]/10',
};

const ICONS: Record<Tone, string> = {
  success: '●', warning: '▲', danger: '■', info: 'ℹ', neutral: '○',
};

export function StatusPill({ tone, label }: { tone: Tone; label: string }) {
  return (
    <span className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-semibold ring-1 ring-inset ${TONES[tone]}`}>
      <span aria-hidden="true" className="text-[0.6rem] leading-none">{ICONS[tone]}</span>
      {label}
    </span>
  );
}

/* --------------------------------------------------------------------------
   AsyncView — the five-state wrapper (08 §7). Loading uses skeleton rows;
   error is role=alert; empty shows guidance. Signature preserved for tests.
   -------------------------------------------------------------------------- */
export function AsyncView({
  isLoading, isError, isEmpty, emptyText, children,
}: {
  isLoading: boolean; isError: boolean; isEmpty?: boolean; emptyText?: string; children: ReactNode;
}) {
  if (isLoading) {
    return (
      <div role="status" aria-live="polite" className="space-y-2 py-2">
        <span className="sr-only">Loading…</span>
        {[0, 1, 2, 3].map((i) => <div key={i} className="skeleton h-11 w-full" />)}
      </div>
    );
  }
  if (isError) return <p role="alert" className="rounded-[var(--radius-md)] bg-[var(--color-absent-bg)] px-4 py-3 text-sm font-medium text-[var(--color-absent)]">Failed to load. Please retry.</p>;
  if (isEmpty) return <EmptyState title={emptyText ?? 'Nothing to show yet.'} />;
  return <>{children}</>;
}

/* --------------------------------------------------------------------------
   Button — primary / secondary / danger / ghost, with loading + disabled.
   -------------------------------------------------------------------------- */
export function Button({
  children, onClick, disabled, loading, variant = 'secondary', type = 'button', size = 'md', className = '',
}: {
  children: ReactNode;
  onClick?: () => void;
  disabled?: boolean;
  loading?: boolean;
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost';
  type?: 'button' | 'submit';
  size?: 'sm' | 'md';
  className?: string;
}) {
  const variants: Record<string, string> = {
    primary: 'bg-[var(--color-brand-600)] text-white hover:bg-[var(--color-brand-700)] shadow-sm',
    secondary: 'bg-white text-[var(--color-ink-soft)] ring-1 ring-inset ring-[var(--color-line)] hover:bg-[var(--color-surface-2)]',
    danger: 'bg-white text-[var(--color-danger)] ring-1 ring-inset ring-[var(--color-danger)]/30 hover:bg-[var(--color-absent-bg)]',
    ghost: 'text-[var(--color-muted)] hover:bg-[var(--color-surface-3)] hover:text-[var(--color-ink)]',
  };
  const sizes: Record<string, string> = { sm: 'px-2.5 py-1 text-xs', md: 'px-3.5 py-2 text-sm' };
  return (
    <button
      type={type}
      onClick={onClick}
      disabled={disabled || loading}
      className={`inline-flex items-center justify-center gap-2 rounded-[var(--radius-md)] font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${variants[variant]} ${sizes[size]} ${className}`}
    >
      {loading && <Spinner />}
      {children}
    </button>
  );
}

function Spinner() {
  return (
    <svg className="h-3.5 w-3.5 animate-spin" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path className="opacity-90" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z" />
    </svg>
  );
}

/* --------------------------------------------------------------------------
   Surfaces — Card, PageHeader
   -------------------------------------------------------------------------- */
export function Card({ children, className = '', pad = true }: { children: ReactNode; className?: string; pad?: boolean }) {
  return (
    <div className={`rounded-[var(--radius-lg)] border border-[var(--color-line)] bg-[var(--color-surface)] shadow-[var(--shadow-card)] ${pad ? 'p-5' : ''} ${className}`}>
      {children}
    </div>
  );
}

export function PageHeader({ title, subtitle, actions }: { title: string; subtitle?: string; actions?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-[var(--color-ink)]">{title}</h1>
        {subtitle && <p className="mt-1 text-sm text-[var(--color-muted)]">{subtitle}</p>}
      </div>
      {actions && <div className="flex items-center gap-2">{actions}</div>}
    </div>
  );
}

/* --------------------------------------------------------------------------
   Stat tile — dashboard KPI (08 §6.2 / §10.2)
   -------------------------------------------------------------------------- */
export function StatTile({
  label, value, tone = 'neutral', icon, hint, delay = 0,
}: {
  label: string; value: ReactNode; tone?: Tone; icon?: ReactNode; hint?: string; delay?: number;
}) {
  const accent: Record<Tone, string> = {
    success: 'var(--color-present)', warning: 'var(--color-late)', danger: 'var(--color-absent)',
    info: 'var(--color-leave)', neutral: 'var(--color-muted)',
  };
  const wash: Record<Tone, string> = {
    success: 'var(--color-present-bg)', warning: 'var(--color-late-bg)', danger: 'var(--color-absent-bg)',
    info: 'var(--color-leave-bg)', neutral: 'var(--color-surface-3)',
  };
  return (
    <div
      className="animate-rise relative overflow-hidden rounded-[var(--radius-lg)] border border-[var(--color-line)] bg-[var(--color-surface)] p-5 shadow-[var(--shadow-card)] transition-shadow hover:shadow-[var(--shadow-raised)]"
      style={{ animationDelay: `${delay}ms` }}
    >
      <span className="absolute inset-x-0 top-0 h-1" style={{ background: accent[tone] }} aria-hidden="true" />
      <div className="flex items-start justify-between">
        <p className="text-xs font-semibold uppercase tracking-wide text-[var(--color-muted)]">{label}</p>
        {icon && (
          <span className="grid h-8 w-8 place-items-center rounded-[var(--radius-md)]" style={{ background: wash[tone], color: accent[tone] }} aria-hidden="true">
            {icon}
          </span>
        )}
      </div>
      <p className="tabular mt-2 text-3xl font-bold tracking-tight text-[var(--color-ink)]">{value}</p>
      {hint && <p className="mt-1 text-xs text-[var(--color-muted-soft)]">{hint}</p>}
    </div>
  );
}

/* --------------------------------------------------------------------------
   Form controls — labelled Field wrapper + styled Input / Select / Textarea
   -------------------------------------------------------------------------- */
export function Field({
  id, label, error, hint, children, className = '',
}: {
  id: string; label: string; error?: string; hint?: string; children: ReactNode; className?: string;
}) {
  return (
    <div className={className}>
      <label htmlFor={id} className="mb-1 block text-sm font-medium text-[var(--color-ink-soft)]">{label}</label>
      {children}
      {hint && !error && <p className="mt-1 text-xs text-[var(--color-muted-soft)]">{hint}</p>}
      {error && <p id={`${id}-err`} role="alert" className="mt-1 text-xs font-medium text-[var(--color-danger)]">{error}</p>}
    </div>
  );
}

const CONTROL =
  'w-full rounded-[var(--radius-md)] border border-[var(--color-line)] bg-white px-3 py-2 text-sm text-[var(--color-ink)] placeholder:text-[var(--color-muted-soft)] transition-colors focus:border-[var(--color-brand-600)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-600)]/20 disabled:bg-[var(--color-surface-3)]';

export function Input({ className = '', ...props }: InputHTMLAttributes<HTMLInputElement>) {
  return <input className={`${CONTROL} ${className}`} {...props} />;
}
export function Select({ className = '', children, ...props }: SelectHTMLAttributes<HTMLSelectElement>) {
  return <select className={`${CONTROL} ${className}`} {...props}>{children}</select>;
}
export function Textarea({ className = '', ...props }: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea className={`${CONTROL} ${className}`} {...props} />;
}

/* --------------------------------------------------------------------------
   Table primitives — consistent, scrollable, sticky-header data tables
   -------------------------------------------------------------------------- */
export function TableWrap({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <div className={`overflow-x-auto rounded-[var(--radius-lg)] border border-[var(--color-line)] bg-[var(--color-surface)] shadow-[var(--shadow-card)] ${className}`}>
      <table className="w-full border-collapse text-left text-sm">{children}</table>
    </div>
  );
}
export function Th({ children, className = '', num = false }: { children?: ReactNode; className?: string; num?: boolean }) {
  return (
    <th scope="col" className={`sticky top-0 z-10 bg-[var(--color-surface-2)] px-4 py-3 text-xs font-semibold uppercase tracking-wide text-[var(--color-muted)] ${num ? 'text-right tabular' : ''} ${className}`}>
      {children}
    </th>
  );
}
export function Td({ children, className = '', num = false }: { children?: ReactNode; className?: string; num?: boolean }) {
  return <td className={`px-4 py-3 align-middle text-[var(--color-ink-soft)] ${num ? 'text-right tabular' : ''} ${className}`}>{children}</td>;
}

/* --------------------------------------------------------------------------
   Empty state — friendly guidance + optional action (08 §11)
   -------------------------------------------------------------------------- */
export function EmptyState({ title, hint, action }: { title: string; hint?: string; action?: ReactNode }) {
  return (
    <div className="flex flex-col items-center justify-center gap-2 rounded-[var(--radius-lg)] border border-dashed border-[var(--color-line)] bg-[var(--color-surface)] px-6 py-12 text-center">
      <div aria-hidden="true" className="grid h-11 w-11 place-items-center rounded-full bg-[var(--color-surface-3)] text-[var(--color-muted-soft)]">
        <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="1.6"><path d="M4 7h16M4 12h16M4 17h10" strokeLinecap="round" /></svg>
      </div>
      <p className="text-sm font-medium text-[var(--color-ink-soft)]">{title}</p>
      {hint && <p className="max-w-sm text-xs text-[var(--color-muted)]">{hint}</p>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
}

/* --------------------------------------------------------------------------
   Toast — success/error feedback (08 §6.2 / §11). Provider + useToast().
   -------------------------------------------------------------------------- */
type Toast = { id: number; message: string; tone: 'success' | 'error' | 'info' };
const ToastCtx = createContext<(message: string, tone?: Toast['tone']) => void>(() => {});
export const useToast = () => useContext(ToastCtx);

let toastSeq = 0;
export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const push = useCallback((message: string, tone: Toast['tone'] = 'success') => {
    const id = ++toastSeq;
    setToasts((t) => [...t, { id, message, tone }]);
  }, []);
  return (
    <ToastCtx.Provider value={push}>
      {children}
      <div className="pointer-events-none fixed bottom-4 right-4 z-50 flex flex-col gap-2" aria-live="polite" aria-atomic="false">
        {toasts.map((t) => <ToastItem key={t.id} toast={t} onDone={() => setToasts((cur) => cur.filter((x) => x.id !== t.id))} />)}
      </div>
    </ToastCtx.Provider>
  );
}

function ToastItem({ toast, onDone }: { toast: Toast; onDone: () => void }) {
  useEffect(() => {
    const timer = setTimeout(onDone, 4000);
    return () => clearTimeout(timer);
  }, [onDone]);
  const tones: Record<Toast['tone'], string> = {
    success: 'border-l-[var(--color-present)]',
    error: 'border-l-[var(--color-absent)]',
    info: 'border-l-[var(--color-leave)]',
  };
  return (
    <div
      role={toast.tone === 'error' ? 'alert' : 'status'}
      className={`animate-rise pointer-events-auto flex min-w-[240px] max-w-sm items-center gap-2 rounded-[var(--radius-md)] border border-[var(--color-line)] border-l-4 bg-white px-4 py-3 text-sm text-[var(--color-ink)] shadow-[var(--shadow-pop)] ${tones[toast.tone]}`}
    >
      {toast.message}
    </div>
  );
}
