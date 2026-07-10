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

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-100">
      <form
        onSubmit={onSubmit}
        className="w-full max-w-sm rounded-lg bg-white p-8 shadow-md"
      >
        <h1 className="mb-6 text-2xl font-semibold text-slate-800">TAMS Sign in</h1>

        <label className="mb-1 block text-sm font-medium text-slate-700" htmlFor="userName">
          Username
        </label>
        <input
          id="userName"
          className="mb-4 w-full rounded border border-slate-300 px-3 py-2 focus:border-blue-500 focus:outline-none"
          {...register('userName', { required: true })}
          autoComplete="username"
        />

        <label className="mb-1 block text-sm font-medium text-slate-700" htmlFor="password">
          Password
        </label>
        <input
          id="password"
          type="password"
          className="mb-4 w-full rounded border border-slate-300 px-3 py-2 focus:border-blue-500 focus:outline-none"
          {...register('password', { required: true })}
          autoComplete="current-password"
        />

        {serverError && (
          <p role="alert" className="mb-4 text-sm text-red-600">
            {serverError}
          </p>
        )}

        <button
          type="submit"
          disabled={formState.isSubmitting}
          className="w-full rounded bg-blue-600 py-2 font-medium text-white hover:bg-blue-700 disabled:opacity-60"
        >
          {formState.isSubmitting ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </div>
  );
}
