import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../api/client';

interface LoginForm {
  userName: string;
  password: string;
}

export function LoginPage() {
  const { login } = useAuth();
  const { register, handleSubmit, formState } = useForm<LoginForm>();
  const [serverError, setServerError] = useState<string | null>(null);
  const [showPassword, setShowPassword] = useState(false);

  const onSubmit = handleSubmit(async (values) => {
    setServerError(null);
    try {
      await login(values.userName, values.password);
    } catch (error) {
      const locked = error instanceof ApiError && error.status === 423;
      setServerError(
        locked
          ? 'Account temporarily locked. Please try again later.'
          : 'Invalid username or password.',
      );
    }
  });

  const control =
    'w-full rounded-[var(--radius-md)] border border-[var(--color-line)] bg-white px-3 py-2.5 text-sm text-[var(--color-ink)] transition-colors focus:border-[var(--color-brand-600)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-600)]/20';

  return (
    // Single centered form — no brand panel, just the sign-in card in the middle.
    <div className="flex min-h-screen items-center justify-center bg-[var(--color-surface-2)] px-6 py-12">
      <form onSubmit={onSubmit} className="w-full max-w-sm rounded-[var(--radius-lg)] border border-[var(--color-line)] bg-[var(--color-surface)] p-8 shadow-[var(--shadow-card)]">
        {/* Brand */}
        <div className="mb-8 flex items-center gap-2.5">
          <span className="grid h-9 w-9 place-items-center rounded-[var(--radius-md)] bg-[var(--color-brand-600)] text-white" aria-hidden="true">
            <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="2"><circle cx="12" cy="12" r="8" /><path d="M12 8v4l2.5 1.5" strokeLinecap="round" /></svg>
          </span>
          <span className="text-lg font-bold text-[var(--color-ink)]">TAMS</span>
        </div>

        <h1 className="text-2xl font-bold tracking-tight text-[var(--color-ink)]">Welcome back</h1>
        <p className="mb-6 mt-1 text-sm text-[var(--color-muted)]">Sign in to your account to continue.</p>

        <div className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-[var(--color-ink-soft)]" htmlFor="userName">
              Username
            </label>
            <input id="userName" className={control} {...register('userName', { required: true })} autoComplete="username" />
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium text-[var(--color-ink-soft)]" htmlFor="password">
              Password
            </label>
            <div className="relative">
              <input
                id="password"
                type={showPassword ? 'text' : 'password'}
                className={`${control} pr-11`}
                {...register('password', { required: true })}
                autoComplete="current-password"
              />
              <PasswordToggle shown={showPassword} onToggle={() => setShowPassword((v) => !v)} />
            </div>
          </div>

          {serverError && (
            <p role="alert" className="rounded-[var(--radius-md)] border border-[var(--color-absent)]/25 bg-[var(--color-absent-bg)] px-3 py-2 text-sm font-medium text-[var(--color-absent)]">
              {serverError}
            </p>
          )}

          <button
            type="submit"
            disabled={formState.isSubmitting}
            className="flex w-full items-center justify-center gap-2 rounded-[var(--radius-md)] bg-[var(--color-brand-600)] py-2.5 font-semibold text-white shadow-sm transition-colors hover:bg-[var(--color-brand-700)] disabled:opacity-60"
          >
            {formState.isSubmitting && (
              <svg className="h-4 w-4 animate-spin" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-90" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z" />
              </svg>
            )}
            {formState.isSubmitting ? 'Signing in…' : 'Sign in'}
          </button>
        </div>
      </form>
    </div>
  );
}

/** Eye toggle that shows/hides a password field. Absolutely positioned inside a
 *  relatively-positioned wrapper; the input must reserve right padding (pr-11). */
function PasswordToggle({ shown, onToggle }: { shown: boolean; onToggle: () => void }) {
  return (
    <button
      type="button"
      onClick={onToggle}
      aria-label={shown ? 'Hide password' : 'Show password'}
      aria-pressed={shown}
      className="absolute inset-y-0 right-0 flex items-center px-3 text-[var(--color-muted)] transition-colors hover:text-[var(--color-ink)] focus:text-[var(--color-brand-600)] focus:outline-none"
    >
      {shown ? (
        // Eye-off
        <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
          <path d="M9.9 4.24A9.1 9.1 0 0 1 12 4c7 0 10 8 10 8a18.5 18.5 0 0 1-2.16 3.19M6.61 6.61A18.5 18.5 0 0 0 2 12s3 8 10 8a9.1 9.1 0 0 0 5.39-1.61" strokeLinecap="round" strokeLinejoin="round" />
          <path d="M14.12 14.12a3 3 0 1 1-4.24-4.24M1 1l22 22" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      ) : (
        // Eye
        <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
          <path d="M2 12s3-8 10-8 10 8 10 8-3 8-10 8-10-8-10-8Z" strokeLinecap="round" strokeLinejoin="round" />
          <circle cx="12" cy="12" r="3" />
        </svg>
      )}
    </button>
  );
}
