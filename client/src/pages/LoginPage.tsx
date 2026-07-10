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
    <div className="grid min-h-screen lg:grid-cols-2">
      {/* Brand panel */}
      <div className="relative hidden overflow-hidden bg-[var(--color-brand-700)] lg:block">
        <div
          className="absolute inset-0 opacity-30"
          aria-hidden="true"
          style={{ background: 'radial-gradient(120% 120% at 15% 10%, #6366f1 0%, transparent 55%), radial-gradient(100% 100% at 90% 90%, #4338ca 0%, transparent 60%)' }}
        />
        <div className="relative flex h-full flex-col justify-between p-12 text-white">
          <div className="flex items-center gap-3">
            <span className="grid h-11 w-11 place-items-center rounded-[var(--radius-lg)] bg-white/15 ring-1 ring-white/25" aria-hidden="true">
              <svg viewBox="0 0 24 24" className="h-6 w-6" fill="none" stroke="currentColor" strokeWidth="2"><circle cx="12" cy="12" r="8" /><path d="M12 8v4l2.5 1.5" strokeLinecap="round" /></svg>
            </span>
            <div>
              <div className="text-lg font-bold tracking-tight">TAMS</div>
              <div className="text-sm text-white/70">Time &amp; Attendance</div>
            </div>
          </div>

          <div className="max-w-md">
            <h2 className="text-3xl font-bold leading-tight tracking-tight text-balance">
              Attendance, shifts &amp; leave — one clear system.
            </h2>
            <p className="mt-3 text-white/75">
              Capture punches from your devices, calculate hours and overtime, manage
              leave, and export payroll — all in real time.
            </p>
          </div>

          <ul className="flex flex-wrap gap-x-6 gap-y-2 text-sm text-white/75">
            <li className="flex items-center gap-2"><Dot /> Live dashboards</li>
            <li className="flex items-center gap-2"><Dot /> Device capture</li>
            <li className="flex items-center gap-2"><Dot /> Payroll export</li>
          </ul>
        </div>
      </div>

      {/* Form side */}
      <div className="flex items-center justify-center bg-[var(--color-surface-2)] px-6 py-12">
        <form onSubmit={onSubmit} className="w-full max-w-sm">
          {/* Compact brand for small screens */}
          <div className="mb-8 flex items-center gap-2.5 lg:hidden">
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
                  className={`${control} pr-16`}
                  {...register('password', { required: true })}
                  autoComplete="current-password"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword((v) => !v)}
                  aria-label={showPassword ? 'Hide password' : 'Show password'}
                  aria-pressed={showPassword}
                  className="absolute inset-y-0 right-0 flex items-center px-3 text-sm font-semibold text-[var(--color-muted)] hover:text-[var(--color-ink)] focus:text-[var(--color-brand-600)] focus:outline-none"
                >
                  {showPassword ? 'Hide' : 'Show'}
                </button>
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
    </div>
  );
}

function Dot() {
  return <span className="h-1.5 w-1.5 rounded-full bg-white/60" aria-hidden="true" />;
}
