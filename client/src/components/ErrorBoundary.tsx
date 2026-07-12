import { Component, type ErrorInfo, type ReactNode } from 'react';

interface Props { children: ReactNode }
interface State { error: Error | null }

/**
 * Catches render-time exceptions anywhere below it so a single component throw
 * doesn't blank the whole SPA. Shows a recoverable fallback with a reload action.
 */
export class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    // Surface to the console for diagnostics; a real deployment would forward this
    // to the error sink (06 §11) with the correlation id.
    console.error('Unhandled UI error:', error, info.componentStack);
  }

  render() {
    if (this.state.error) {
      return (
        <div className="flex min-h-screen items-center justify-center bg-[var(--color-surface-2)] p-6">
          <div className="w-full max-w-md rounded-[var(--radius-lg)] border border-[var(--color-line)] bg-[var(--color-surface)] p-6 text-center shadow-[var(--shadow-card)]">
            <div aria-hidden="true" className="mx-auto mb-3 grid h-11 w-11 place-items-center rounded-full bg-[var(--color-absent-bg)] text-[var(--color-absent)]">
              <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="2"><path d="M12 9v4M12 17h.01M10.3 3.9 2.4 18a2 2 0 0 0 1.7 3h15.8a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z" strokeLinecap="round" strokeLinejoin="round" /></svg>
            </div>
            <h1 className="text-lg font-bold text-[var(--color-ink)]">Something went wrong</h1>
            <p className="mt-1 text-sm text-[var(--color-muted)]">
              An unexpected error occurred while displaying this page. Reloading usually fixes it.
            </p>
            <button
              onClick={() => window.location.reload()}
              className="mt-4 rounded-[var(--radius-md)] bg-[var(--color-brand-600)] px-4 py-2 text-sm font-semibold text-white hover:bg-[var(--color-brand-700)]"
            >
              Reload page
            </button>
          </div>
        </div>
      );
    }
    return this.props.children;
  }
}
