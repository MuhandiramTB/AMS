import type { ReactNode, InputHTMLAttributes, SelectHTMLAttributes, TextareaHTMLAttributes } from 'react';
import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { errorStatus } from '../api/client';

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
  isLoading, isError, isEmpty, emptyText, error, children,
}: {
  isLoading: boolean; isError: boolean; isEmpty?: boolean; emptyText?: string; error?: unknown; children: ReactNode;
}) {
  if (isLoading) {
    return (
      <div role="status" aria-live="polite" className="space-y-2 py-2">
        <span className="sr-only">Loading…</span>
        {[0, 1, 2, 3].map((i) => <div key={i} className="skeleton h-11 w-full" />)}
      </div>
    );
  }
  if (isError) {
    // A 403 is not a failure — it means the signed-in user isn't permitted to see
    // this data (e.g. a role with no all-rows scope and no linked employee).
    const status = errorStatus(error);
    if (status === 403) {
      return (
        <div role="status" className="rounded-[var(--radius-md)] border border-[var(--color-line)] bg-[var(--color-surface)] px-4 py-6 text-center">
          <p className="text-sm font-medium text-[var(--color-ink-soft)]">Nothing to show for your account here.</p>
          <p className="mx-auto mt-1 max-w-md text-xs text-[var(--color-muted)]">
            Your role can only see its own records, and your login isn’t linked to an
            employee record yet. An administrator can link it on the Users page.
          </p>
        </div>
      );
    }
    // Non-403: give a status-appropriate message and, when available, a support
    // reference (correlation id) so a report is traceable rather than opaque.
    const detail =
      status === 404 ? 'We couldn’t find what you were looking for.'
      : status === 409 ? 'This changed while you were viewing it. Refresh and try again.'
      : status && status >= 500 ? 'The server ran into a problem. Please retry shortly.'
      : 'Failed to load. Please retry.';
    const correlationId =
      error && typeof error === 'object' && 'correlationId' in error
        ? (error as { correlationId?: string }).correlationId
        : undefined;
    return (
      <div role="alert" className="rounded-[var(--radius-md)] bg-[var(--color-absent-bg)] px-4 py-3 text-sm text-[var(--color-absent)]">
        <p className="font-medium">{detail}</p>
        {correlationId && (
          <p className="mt-0.5 text-xs text-[var(--color-absent)]/80">Reference: <span className="font-mono">{correlationId}</span></p>
        )}
      </div>
    );
  }
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
  id, label, error, hint, required = false, children, className = '',
}: {
  id: string; label: string; error?: string; hint?: string; required?: boolean; children: ReactNode; className?: string;
}) {
  return (
    <div className={className}>
      {/* The asterisk sits OUTSIDE the <label> so the label's accessible name
          stays exactly the field name (getByLabelText matches on label text). */}
      <div className="mb-1 flex items-center gap-0.5">
        <label htmlFor={id} className="block text-sm font-medium text-[var(--color-ink-soft)]">{label}</label>
        {required && <span className="text-[var(--color-danger)]" title="Required" aria-hidden="true">*</span>}
      </div>
      {children}
      {hint && !error && <p className="mt-1 text-xs text-[var(--color-muted-soft)]">{hint}</p>}
      {error && (
        <p id={`${id}-err`} role="alert" className="mt-1 flex items-center gap-1 text-xs font-medium text-[var(--color-danger)]">
          <svg viewBox="0 0 24 24" className="h-3.5 w-3.5 shrink-0" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><circle cx="12" cy="12" r="9" /><path d="M12 8v5M12 16h.01" strokeLinecap="round" /></svg>
          {error}
        </p>
      )}
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
   Table system — the shared, maintainable way every list page renders data.

   • Each module gets a DISTINCT header colour via the `module` prop (tokens in
     index.css: --hdr-<module>-bg / -fg). Pass the same module to <DataTable> and
     it flows to the header automatically.
   • DataTable pins the header + toolbar and scrolls ONLY the rows inside a
     fixed-height body (maxBodyHeight, default ~20 rows). Pagination sits below,
     always visible.
   • Low-level <TableWrap>/<Th>/<Td> remain for bespoke tables (e.g. dashboard).
   -------------------------------------------------------------------------- */

export type TableModule = 'employees' | 'departments' | 'attendance' | 'shifts' | 'leave' | 'devices' | 'default';

function headerStyle(module: TableModule): { background: string; color: string } {
  if (module === 'default') return { background: 'var(--color-surface-2)', color: 'var(--color-muted)' };
  return { background: `var(--hdr-${module}-bg)`, color: `var(--hdr-${module}-fg)` };
}

export function TableWrap({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <div className={`overflow-x-auto rounded-[var(--radius-lg)] border border-[var(--color-line)] bg-[var(--color-surface)] shadow-[var(--shadow-card)] ${className}`}>
      <table className="w-full border-collapse text-left text-sm">{children}</table>
    </div>
  );
}

export function Th({ children, className = '', num = false, module = 'default' }: { children?: ReactNode; className?: string; num?: boolean; module?: TableModule }) {
  return (
    <th
      scope="col"
      style={headerStyle(module)}
      className={`sticky top-0 z-10 px-4 py-3 text-xs font-semibold uppercase tracking-wide ${num ? 'text-right tabular' : ''} ${className}`}
    >
      {children}
    </th>
  );
}

export function Td({ children, className = '', num = false, colSpan }: { children?: ReactNode; className?: string; num?: boolean; colSpan?: number }) {
  return <td colSpan={colSpan} className={`px-4 py-3 align-middle text-[var(--color-ink-soft)] ${num ? 'text-right tabular' : ''} ${className}`}>{children}</td>;
}

/**
 * DataTable — the standard list table. Header row is coloured per `module`,
 * stays sticky; only the rows scroll (fixed-height body). Provide the header
 * cells via `head` and the rows via `children`.
 */
export function DataTable({
  head,
  children,
  maxBodyHeight = '60vh',
  className = '',
}: {
  head: ReactNode;       // a <tr> of <Th module={module}> cells
  children: ReactNode;   // the <tr> rows
  maxBodyHeight?: string;
  className?: string;
}) {
  return (
    <div className={`overflow-hidden rounded-[var(--radius-lg)] border border-[var(--color-line)] bg-[var(--color-surface)] shadow-[var(--shadow-card)] ${className}`}>
      <div className="overflow-auto" style={{ maxHeight: maxBodyHeight }}>
        <table className="w-full border-collapse text-left text-sm">
          <thead>{head}</thead>
          <tbody>{children}</tbody>
        </table>
      </div>
    </div>
  );
}

/** A standard table row with hover + bottom divider. */
export function Tr({ children, className = '', selected = false }: { children: ReactNode; className?: string; selected?: boolean }) {
  return (
    <tr className={`border-t border-[var(--color-line-soft)] transition-colors ${selected ? 'bg-[var(--color-brand-50)]' : 'hover:bg-[var(--color-surface-2)]'} ${className}`}>
      {children}
    </tr>
  );
}

/* --------------------------------------------------------------------------
   Toolbar + Search + Filters — the standard filter section above a table.
   -------------------------------------------------------------------------- */

/** A filter/search bar shell that sits above a DataTable. */
export function Toolbar({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <div className={`flex flex-wrap items-end gap-3 rounded-[var(--radius-lg)] border border-[var(--color-line)] bg-[var(--color-surface)] p-3.5 shadow-[var(--shadow-card)] ${className}`}>
      {children}
    </div>
  );
}

/** Search box with a magnifier icon and a clear button. Controlled. */
export function SearchInput({
  value, onChange, placeholder = 'Search…', label = 'Search', className = '',
}: {
  value: string; onChange: (v: string) => void; placeholder?: string; label?: string; className?: string;
}) {
  const id = `search-${label.replace(/\s+/g, '-').toLowerCase()}`;
  return (
    <div className={`min-w-56 flex-1 ${className}`}>
      <label htmlFor={id} className="mb-1 block text-sm font-medium text-[var(--color-ink-soft)]">{label}</label>
      <div className="relative">
        <span className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3 text-[var(--color-muted-soft)]" aria-hidden="true">
          <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="2"><circle cx="11" cy="11" r="7" /><path d="m21 21-4.3-4.3" strokeLinecap="round" /></svg>
        </span>
        <input
          id={id}
          type="search"
          value={value}
          placeholder={placeholder}
          onChange={(e) => onChange(e.target.value)}
          className={`${CONTROL} pl-9 ${value ? 'pr-9' : ''}`}
        />
        {value && (
          <button
            type="button"
            onClick={() => onChange('')}
            aria-label="Clear search"
            className="absolute inset-y-0 right-0 flex items-center pr-3 text-[var(--color-muted-soft)] hover:text-[var(--color-ink)]"
          >
            <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><path d="M18 6 6 18M6 6l12 12" strokeLinecap="round" /></svg>
          </button>
        )}
      </div>
    </div>
  );
}

/* --------------------------------------------------------------------------
   Pagination — shared, always-visible pager. Default page size lives here.
   -------------------------------------------------------------------------- */
export const DEFAULT_PAGE_SIZE = 20;

export function Pagination({
  page, totalPages, totalCount, onPage,
}: {
  page: number; totalPages: number; totalCount?: number; onPage: (p: number) => void;
}) {
  const pages = Math.max(totalPages, 1);
  return (
    <nav className="flex flex-wrap items-center justify-between gap-3 pt-1 text-sm" aria-label="Pagination">
      <span className="text-[var(--color-muted)]">
        Page <span className="font-semibold text-[var(--color-ink-soft)]">{page}</span> of {pages}
        {typeof totalCount === 'number' && <span className="text-[var(--color-muted-soft)]"> · {totalCount} total</span>}
      </span>
      <div className="flex items-center gap-2">
        <Button size="sm" disabled={page <= 1} onClick={() => onPage(page - 1)}>Prev</Button>
        <Button size="sm" disabled={page >= pages} onClick={() => onPage(page + 1)}>Next</Button>
      </div>
    </nav>
  );
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
   Form-level error banner — renders a mutation's top-level message plus its
   support/correlation id, so a failed submit shows something actionable and
   traceable rather than a silent no-op. (08 §11.)
   -------------------------------------------------------------------------- */
export function FormError({ error }: { error: unknown }) {
  if (!error) return null;
  const message =
    error instanceof Error && error.message ? error.message : 'Something went wrong. Please try again.';
  const correlationId =
    error && typeof error === 'object' && 'correlationId' in error
      ? (error as { correlationId?: string }).correlationId
      : undefined;
  return (
    <div role="alert" className="rounded-[var(--radius-md)] border border-[var(--color-danger)]/30 bg-[var(--color-absent-bg)] px-3 py-2 text-sm text-[var(--color-absent)]">
      <div className="flex items-start gap-2">
        <svg viewBox="0 0 24 24" className="mt-0.5 h-4 w-4 shrink-0" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><circle cx="12" cy="12" r="9" /><path d="M12 8v5M12 16h.01" strokeLinecap="round" /></svg>
        <div>
          <p className="font-medium">{message}</p>
          {correlationId && (
            <p className="mt-0.5 text-xs text-[var(--color-absent)]/80">
              Reference: <span className="font-mono">{correlationId}</span>
            </p>
          )}
        </div>
      </div>
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
      // Errors interrupt (assertive); success/info wait their turn (polite).
      aria-live={toast.tone === 'error' ? 'assertive' : 'polite'}
      className={`animate-rise pointer-events-auto flex min-w-[240px] max-w-sm items-center gap-2 rounded-[var(--radius-md)] border border-[var(--color-line)] border-l-4 bg-white px-4 py-3 text-sm text-[var(--color-ink)] shadow-[var(--shadow-pop)] ${tones[toast.tone]}`}
    >
      {toast.message}
    </div>
  );
}

/* --------------------------------------------------------------------------
   Modal — accessible centered dialog with focus trap, Escape-to-close and
   focus restore. Used for edit forms and confirmations (08 §6.2). Reuses the
   same dialog semantics as the attendance correction drawer.
   -------------------------------------------------------------------------- */
export function Modal({
  title, onClose, children, footer, size = 'md',
}: {
  title: string; onClose: () => void; children: ReactNode; footer?: ReactNode; size?: 'sm' | 'md' | 'lg';
}) {
  const ref = useRef<HTMLDivElement>(null);
  const titleId = useRef(`modal-${Math.round(performance.now())}`).current;

  useEffect(() => {
    const previouslyFocused = document.activeElement as HTMLElement | null;
    // Focus the first focusable control in the dialog.
    ref.current?.querySelector<HTMLElement>('input, select, textarea, button')?.focus();

    function onKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') { e.preventDefault(); onClose(); return; }
      if (e.key !== 'Tab') return;
      const f = ref.current?.querySelectorAll<HTMLElement>('a[href], button:not([disabled]), input, textarea, select, [tabindex]:not([tabindex="-1"])');
      if (!f || f.length === 0) return;
      const first = f[0]; const last = f[f.length - 1];
      if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
      else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
    }
    document.addEventListener('keydown', onKeyDown);
    return () => { document.removeEventListener('keydown', onKeyDown); previouslyFocused?.focus(); };
  }, [onClose]);

  const widths: Record<string, string> = { sm: 'max-w-sm', md: 'max-w-lg', lg: 'max-w-2xl' };

  // Render through a portal to <body> so the dialog is never hidden or clipped by an
  // ancestor (e.g. a table cell with display:none, or an overflow-hidden container).
  return createPortal(
    <div className="fixed inset-0 z-40 flex items-center justify-center p-4">
      {/* Backdrop — click to dismiss. */}
      <div className="absolute inset-0 bg-[var(--color-ink)]/40 backdrop-blur-[1px]" onClick={onClose} aria-hidden="true" />
      <div
        ref={ref}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className={`animate-rise relative w-full ${widths[size]} rounded-[var(--radius-lg)] border border-[var(--color-line)] bg-[var(--color-surface)] shadow-[var(--shadow-pop)]`}
      >
        <div className="flex items-center justify-between border-b border-[var(--color-line-soft)] px-5 py-3.5">
          <h2 id={titleId} className="text-base font-bold tracking-tight text-[var(--color-ink)]">{title}</h2>
          <button onClick={onClose} aria-label="Close" className="rounded-[var(--radius-md)] p-1 text-[var(--color-muted-soft)] transition-colors hover:bg-[var(--color-surface-3)] hover:text-[var(--color-ink)]">
            <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><path d="M18 6 6 18M6 6l12 12" strokeLinecap="round" /></svg>
          </button>
        </div>
        <div className="px-5 py-4">{children}</div>
        {footer && <div className="flex justify-end gap-2 border-t border-[var(--color-line-soft)] px-5 py-3.5">{footer}</div>}
      </div>
    </div>,
    document.body,
  );
}

/**
 * ConfirmDialog — a small confirmation modal for destructive/irreversible
 * actions. `tone="danger"` styles the confirm button as destructive.
 */
export function ConfirmDialog({
  title, message, confirmLabel = 'Confirm', cancelLabel = 'Cancel', tone = 'primary', loading = false, onConfirm, onCancel,
}: {
  title: string; message: ReactNode; confirmLabel?: string; cancelLabel?: string;
  tone?: 'primary' | 'danger'; loading?: boolean; onConfirm: () => void; onCancel: () => void;
}) {
  return (
    <Modal
      title={title}
      onClose={onCancel}
      size="sm"
      footer={
        <>
          <Button onClick={onCancel}>{cancelLabel}</Button>
          <Button variant={tone} loading={loading} onClick={onConfirm}>{confirmLabel}</Button>
        </>
      }
    >
      <p className="text-sm text-[var(--color-ink-soft)]">{message}</p>
    </Modal>
  );
}

/* --------------------------------------------------------------------------
   RowActions — a kebab (⋮) menu of per-row actions for a data table.
   -------------------------------------------------------------------------- */
export type RowAction = { label: string; onClick: () => void; tone?: 'default' | 'danger'; icon?: ReactNode };

export function RowActions({ actions, label = 'Row actions' }: { actions: RowAction[]; label?: string }) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    function onDoc(e: MouseEvent) { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false); }
    function onKey(e: KeyboardEvent) { if (e.key === 'Escape') setOpen(false); }
    document.addEventListener('mousedown', onDoc);
    document.addEventListener('keydown', onKey);
    return () => { document.removeEventListener('mousedown', onDoc); document.removeEventListener('keydown', onKey); };
  }, [open]);

  if (actions.length === 0) return null;

  return (
    <div ref={ref} className="relative inline-block text-left">
      <button
        type="button"
        aria-label={label}
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((v) => !v)}
        className="rounded-[var(--radius-md)] p-1.5 text-[var(--color-muted)] transition-colors hover:bg-[var(--color-surface-3)] hover:text-[var(--color-ink)]"
      >
        <svg viewBox="0 0 24 24" className="h-4 w-4" fill="currentColor" aria-hidden="true"><circle cx="12" cy="5" r="1.6" /><circle cx="12" cy="12" r="1.6" /><circle cx="12" cy="19" r="1.6" /></svg>
      </button>
      {open && (
        <div role="menu" className="absolute right-0 z-20 mt-1 min-w-40 overflow-hidden rounded-[var(--radius-md)] border border-[var(--color-line)] bg-[var(--color-surface)] py-1 shadow-[var(--shadow-pop)]">
          {actions.map((a) => (
            <button
              key={a.label}
              role="menuitem"
              type="button"
              onClick={() => { setOpen(false); a.onClick(); }}
              className={`flex w-full items-center gap-2 px-3.5 py-2 text-left text-sm transition-colors hover:bg-[var(--color-surface-2)] ${a.tone === 'danger' ? 'text-[var(--color-danger)]' : 'text-[var(--color-ink-soft)]'}`}
            >
              {a.icon}
              {a.label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

/* --------------------------------------------------------------------------
   IconButton — a compact, accessible icon-only action button for table rows.
   Always carries an aria-label + tooltip title so it is not icon-alone to AT.
   -------------------------------------------------------------------------- */
export function IconButton({
  label, onClick, icon, tone = 'default', disabled = false,
}: {
  label: string; onClick: () => void; icon: ReactNode; tone?: 'default' | 'danger' | 'success'; disabled?: boolean;
}) {
  const tones: Record<string, string> = {
    default: 'text-[var(--color-muted)] hover:bg-[var(--color-surface-3)] hover:text-[var(--color-brand-700)]',
    danger: 'text-[var(--color-muted)] hover:bg-[var(--color-absent-bg)] hover:text-[var(--color-danger)]',
    success: 'text-[var(--color-muted)] hover:bg-[var(--color-present-bg)] hover:text-[var(--color-present)]',
  };
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      aria-label={label}
      title={label}
      className={`inline-grid h-8 w-8 place-items-center rounded-[var(--radius-md)] transition-colors disabled:opacity-40 ${tones[tone]}`}
    >
      {icon}
    </button>
  );
}

/** Common 18px stroke icons for row actions. */
export const ActionIcons = {
  edit: (
    <svg viewBox="0 0 24 24" className="h-[18px] w-[18px]" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path d="M12 20h9M16.5 3.5a2.1 2.1 0 013 3L7 19l-4 1 1-4z" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  ),
  deactivate: (
    <svg viewBox="0 0 24 24" className="h-[18px] w-[18px]" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path d="M18.36 6.64a9 9 0 11-12.73 0M12 2v10" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  ),
  activate: (
    <svg viewBox="0 0 24 24" className="h-[18px] w-[18px]" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path d="M20 6L9 17l-5-5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  ),
  sync: (
    <svg viewBox="0 0 24 24" className="h-[18px] w-[18px]" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path d="M21 2v6h-6M3 22v-6h6M21 8a9 9 0 00-15-3.5L3 8M3 16a9 9 0 0015 3.5l3-3.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  ),
  test: (
    <svg viewBox="0 0 24 24" className="h-[18px] w-[18px]" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path d="M5 12.5l4 4 10-10" strokeLinecap="round" strokeLinejoin="round" /><circle cx="12" cy="12" r="10" opacity="0.35" />
    </svg>
  ),
  reconcile: (
    <svg viewBox="0 0 24 24" className="h-[18px] w-[18px]" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path d="M9 11l3 3 8-8M4 4v7h7M4 20a8 8 0 0014-4" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  ),
  power: (
    <svg viewBox="0 0 24 24" className="h-[18px] w-[18px]" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path d="M18.36 6.64a9 9 0 11-12.73 0M12 2v10" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  ),
} as const;

/* --------------------------------------------------------------------------
   SearchableSelect — a dropdown that opens a panel with a SEARCH BOX at the top
   and the filtered options below. Looks/behaves like a normal select, but you
   can type to narrow a long list. Controlled via `value` + `onChange`.
   -------------------------------------------------------------------------- */
export type SelectOption = { value: string; label: string };

export function SearchableSelect({
  options, value, onChange, placeholder = 'Select…', searchPlaceholder = 'Search…', id, emptyText = 'No matches',
  onSearch, truncated = false,
}: {
  options: SelectOption[];
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  searchPlaceholder?: string;
  id?: string;
  emptyText?: string;
  /** When provided, typing drives a SERVER-side search instead of filtering the
      given options in-place — needed when the roster exceeds the API page cap. */
  onSearch?: (query: string) => void;
  /** Show a "refine your search" hint when the option list is server-capped. */
  truncated?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [active, setActive] = useState(0);
  const ref = useRef<HTMLDivElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);
  const listId = useRef(`ssel-${Math.round(performance.now())}`).current;

  const selected = options.find((o) => o.value === value);
  // In server-search mode the parent already returns the matching rows, so don't
  // filter again locally (that would hide rows whose label formatting differs).
  const filtered = onSearch || !query.trim()
    ? options
    : options.filter((o) => o.label.toLowerCase().includes(query.trim().toLowerCase()));

  useEffect(() => { setActive(0); }, [query, open]);

  useEffect(() => {
    if (!open) return;
    searchRef.current?.focus();
    function onDoc(e: MouseEvent) { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false); }
    document.addEventListener('mousedown', onDoc);
    return () => document.removeEventListener('mousedown', onDoc);
  }, [open]);

  // Keyboard: arrows move the active option, Enter selects it, Escape closes.
  function onSearchKey(e: React.KeyboardEvent) {
    if (e.key === 'ArrowDown') { e.preventDefault(); setActive((i) => Math.min(i + 1, filtered.length - 1)); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setActive((i) => Math.max(i - 1, 0)); }
    else if (e.key === 'Enter') { e.preventDefault(); const o = filtered[active]; if (o) { onChange(o.value); setOpen(false); } }
    else if (e.key === 'Escape') { e.preventDefault(); setOpen(false); }
  }

  return (
    <div ref={ref} className="relative">
      {/* The closed control — looks like a normal select. */}
      <button
        id={id}
        type="button"
        aria-haspopup="listbox"
        aria-expanded={open}
        onClick={() => { setOpen((v) => !v); setQuery(''); }}
        className={`flex w-full items-center justify-between gap-2 rounded-[var(--radius-md)] border border-[var(--color-line)] bg-white px-3 py-2 text-left text-sm transition-colors focus:border-[var(--color-brand-600)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-600)]/20 ${selected ? 'text-[var(--color-ink)]' : 'text-[var(--color-muted-soft)]'}`}
      >
        <span className="truncate">{selected?.label ?? placeholder}</span>
        <svg viewBox="0 0 24 24" className="h-4 w-4 shrink-0 text-[var(--color-muted-soft)]" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><path d="M6 9l6 6 6-6" strokeLinecap="round" strokeLinejoin="round" /></svg>
      </button>

      {open && (
        <div className="absolute z-30 mt-1 w-full overflow-hidden rounded-[var(--radius-md)] border border-[var(--color-line)] bg-[var(--color-surface)] shadow-[var(--shadow-pop)]">
          {/* Search box inside the dropdown panel. */}
          <div className="border-b border-[var(--color-line-soft)] p-2">
            <div className="relative">
              <span className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-2.5 text-[var(--color-muted-soft)]" aria-hidden="true">
                <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="2"><circle cx="11" cy="11" r="7" /><path d="m21 21-4.3-4.3" strokeLinecap="round" /></svg>
              </span>
              <input
                ref={searchRef}
                type="text"
                role="combobox"
                aria-expanded="true"
                aria-controls={listId}
                aria-activedescendant={filtered[active] ? `${listId}-opt-${active}` : undefined}
                aria-autocomplete="list"
                value={query}
                placeholder={searchPlaceholder}
                onChange={(e) => { setQuery(e.target.value); onSearch?.(e.target.value); }}
                onKeyDown={onSearchKey}
                className="w-full rounded-[var(--radius-md)] border border-[var(--color-line)] bg-white py-1.5 pl-8 pr-3 text-sm focus:border-[var(--color-brand-600)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-600)]/20"
              />
            </div>
          </div>
          <ul id={listId} role="listbox" className="max-h-56 overflow-auto py-1">
            {filtered.length === 0 && <li className="px-3 py-2 text-sm text-[var(--color-muted-soft)]">{emptyText}</li>}
            {filtered.map((opt, i) => (
              <li
                key={opt.value}
                id={`${listId}-opt-${i}`}
                role="option"
                aria-selected={opt.value === value}
                onMouseEnter={() => setActive(i)}
                onClick={() => { onChange(opt.value); setOpen(false); }}
                className={`cursor-pointer px-3 py-2 text-sm ${
                  i === active ? 'bg-[var(--color-surface-2)]' : ''
                } ${
                  opt.value === value ? 'bg-[var(--color-brand-50)] font-medium text-[var(--color-brand-700)]' : 'text-[var(--color-ink-soft)]'
                }`}
              >
                {opt.label}
              </li>
            ))}
          </ul>
          {truncated && (
            <div className="border-t border-[var(--color-line-soft)] px-3 py-1.5 text-xs text-[var(--color-muted-soft)]">
              Showing the first {filtered.length}. Type to narrow the list.
            </div>
          )}
        </div>
      )}
    </div>
  );
}
