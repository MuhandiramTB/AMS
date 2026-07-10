import type { ReactNode } from 'react';

/**
 * Status pill — always carries a text label + icon, never colour alone, so it is
 * distinguishable for colour-blind users and in greyscale. (08 §12.)
 */
type Tone = 'success' | 'warning' | 'danger' | 'info' | 'neutral';

const TONES: Record<Tone, string> = {
  success: 'bg-green-100 text-green-800',
  warning: 'bg-amber-100 text-amber-800',
  danger: 'bg-red-100 text-red-800',
  info: 'bg-blue-100 text-blue-800',
  neutral: 'bg-slate-100 text-slate-700',
};

const ICONS: Record<Tone, string> = {
  success: '●',
  warning: '▲',
  danger: '■',
  info: 'ℹ',
  neutral: '○',
};

export function StatusPill({ tone, label }: { tone: Tone; label: string }) {
  return (
    <span className={`inline-flex items-center gap-1 rounded px-2 py-0.5 text-xs font-medium ${TONES[tone]}`}>
      <span aria-hidden="true">{ICONS[tone]}</span>
      {label}
    </span>
  );
}

/** Standard async-view state wrapper: handles loading / error / empty (08 §7). */
export function AsyncView({
  isLoading,
  isError,
  isEmpty,
  emptyText,
  children,
}: {
  isLoading: boolean;
  isError: boolean;
  isEmpty?: boolean;
  emptyText?: string;
  children: ReactNode;
}) {
  if (isLoading) return <p role="status" aria-live="polite" className="py-4 text-slate-500">Loading…</p>;
  if (isError) return <p role="alert" className="py-4 text-red-600">Failed to load. Please retry.</p>;
  if (isEmpty) return <p className="py-4 text-slate-400">{emptyText ?? 'Nothing to show yet.'}</p>;
  return <>{children}</>;
}

export function Button({
  children,
  onClick,
  disabled,
  variant = 'secondary',
  type = 'button',
}: {
  children: ReactNode;
  onClick?: () => void;
  disabled?: boolean;
  variant?: 'primary' | 'secondary' | 'danger';
  type?: 'button' | 'submit';
}) {
  const styles: Record<string, string> = {
    primary: 'bg-blue-600 text-white hover:bg-blue-700',
    secondary: 'border border-slate-300 text-slate-700 hover:bg-slate-100',
    danger: 'border border-red-300 text-red-700 hover:bg-red-50',
  };
  return (
    <button
      type={type}
      onClick={onClick}
      disabled={disabled}
      className={`rounded px-3 py-1 text-sm disabled:opacity-50 ${styles[variant]}`}
    >
      {children}
    </button>
  );
}
