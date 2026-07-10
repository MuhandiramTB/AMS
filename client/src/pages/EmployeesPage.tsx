import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useCreateEmployee, useDepartments, useEmployees, type CreateEmployeeInput } from '../api/hooks';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../api/client';

const FIELD_NAMES: (keyof CreateEmployeeInput)[] = [
  'employeeNo',
  'firstName',
  'lastName',
  'email',
  'primaryDepartmentId',
];

/** Match a server field key (often PascalCase) to a form field (camelCase). */
function toFormField(serverKey: string): keyof CreateEmployeeInput | null {
  const lower = serverKey.toLowerCase();
  return FIELD_NAMES.find((f) => f.toLowerCase() === lower) ?? null;
}

export function EmployeesPage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission('Employee.Write');
  const [page, setPage] = useState(1);
  const pageSize = 10;

  const { data, isLoading, isError, isFetching, isPlaceholderData } = useEmployees(page, pageSize);
  const { data: departments } = useDepartments();
  const createEmployee = useCreateEmployee();
  const { register, handleSubmit, reset, setError, formState } = useForm<CreateEmployeeInput>();
  const [formError, setFormError] = useState<string | null>(null);

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await createEmployee.mutateAsync({
        ...values,
        primaryDepartmentId: Number(values.primaryDepartmentId),
      });
      reset();
    } catch (error) {
      if (error instanceof ApiError) {
        if (error.fieldErrors) {
          // Map RFC 9457 field errors back onto the specific fields (08 §9).
          let mappedAny = false;
          for (const [key, messages] of Object.entries(error.fieldErrors)) {
            const field = toFormField(key);
            if (field) {
              setError(field, { type: 'server', message: messages.join(' ') });
              mappedAny = true;
            }
          }
          if (!mappedAny) setFormError(Object.values(error.fieldErrors).flat().join(' '));
        } else {
          setFormError(error.message);
        }
      } else {
        setFormError('An unexpected error occurred.');
      }
    }
  });

  return (
    <div>
      <h1 className="mb-4 text-2xl font-semibold text-slate-800">Employees</h1>

      {canWrite && (
        <form onSubmit={onSubmit} className="mb-6 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
          <Field id="employeeNo" label="Employee No" error={formState.errors.employeeNo?.message}>
            <input id="employeeNo" aria-describedby={formState.errors.employeeNo ? 'employeeNo-err' : undefined} className="w-full rounded border border-slate-300 px-2 py-1" {...register('employeeNo', { required: 'Required' })} />
          </Field>
          <Field id="firstName" label="First name" error={formState.errors.firstName?.message}>
            <input id="firstName" aria-describedby={formState.errors.firstName ? 'firstName-err' : undefined} className="w-full rounded border border-slate-300 px-2 py-1" {...register('firstName', { required: 'Required' })} />
          </Field>
          <Field id="lastName" label="Last name" error={formState.errors.lastName?.message}>
            <input id="lastName" aria-describedby={formState.errors.lastName ? 'lastName-err' : undefined} className="w-full rounded border border-slate-300 px-2 py-1" {...register('lastName', { required: 'Required' })} />
          </Field>
          <Field id="email" label="Email" error={formState.errors.email?.message}>
            <input id="email" aria-describedby={formState.errors.email ? 'email-err' : undefined} className="w-full rounded border border-slate-300 px-2 py-1" {...register('email')} />
          </Field>
          <Field id="primaryDepartmentId" label="Department" error={formState.errors.primaryDepartmentId?.message}>
            <select id="primaryDepartmentId" aria-describedby={formState.errors.primaryDepartmentId ? 'primaryDepartmentId-err' : undefined} className="w-full rounded border border-slate-300 px-2 py-1" {...register('primaryDepartmentId', { required: 'Required' })}>
              <option value="">Department…</option>
              {departments?.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </Field>
          <div className="col-span-2 sm:col-span-3 lg:col-span-5">
            <button
              type="submit"
              disabled={formState.isSubmitting || createEmployee.isPending}
              className="rounded bg-blue-600 px-4 py-1.5 text-white hover:bg-blue-700 disabled:opacity-60"
            >
              {createEmployee.isPending ? 'Adding…' : 'Add employee'}
            </button>
            {formError && <span role="alert" className="ml-3 text-sm text-red-600">{formError}</span>}
          </div>
        </form>
      )}

      {isLoading && <p className="text-slate-500">Loading…</p>}
      {isError && <p role="alert" className="text-red-600">Failed to load employees.</p>}

      {data && (
        <>
          {/* Stale/refreshing indicator while paging (08 §7). Announced politely. */}
          <div role="status" aria-live="polite" className="mb-2 h-4 text-xs text-slate-400">
            {isFetching && isPlaceholderData ? 'Refreshing…' : ''}
          </div>
          <table className={`w-full border-collapse text-left text-sm ${isPlaceholderData ? 'opacity-60' : ''}`}>
            <thead>
              <tr className="border-b border-slate-200 text-slate-500">
                <th scope="col" className="py-2">Employee No</th>
                <th scope="col" className="py-2">Name</th>
                <th scope="col" className="py-2">Email</th>
                <th scope="col" className="py-2">Status</th>
              </tr>
            </thead>
            <tbody>
              {data.items.length === 0 && (
                <tr><td colSpan={4} className="py-4 text-slate-400">No employees yet.</td></tr>
              )}
              {data.items.map((e) => (
                <tr key={e.id} className="border-b border-slate-100">
                  <td className="py-2 font-mono">{e.employeeNo}</td>
                  <td className="py-2">{e.firstName} {e.lastName}</td>
                  <td className="py-2">{e.email ?? '—'}</td>
                  <td className="py-2">{e.status}</td>
                </tr>
              ))}
            </tbody>
          </table>

          <div className="mt-4 flex items-center gap-3 text-sm">
            <button
              disabled={page <= 1}
              onClick={() => setPage((p) => p - 1)}
              className="rounded border border-slate-300 px-3 py-1 disabled:opacity-40"
            >
              Prev
            </button>
            <span className="text-slate-500">
              Page {data.page} of {data.totalPages || 1} ({data.totalCount} total)
            </span>
            <button
              disabled={page >= data.totalPages}
              onClick={() => setPage((p) => p + 1)}
              className="rounded border border-slate-300 px-3 py-1 disabled:opacity-40"
            >
              Next
            </button>
          </div>
        </>
      )}
    </div>
  );
}

/**
 * Small labelled field wrapper. The label is programmatically associated with its
 * control via htmlFor/id, and the error is linked with a stable id so aria-describedby
 * on the input announces it. (WCAG 1.3.1 / 3.3.2 / 4.1.2, 08 §12.)
 */
function Field({ id, label, error, children }: { id: string; label: string; error?: string; children: React.ReactNode }) {
  return (
    <div>
      <label htmlFor={id} className="block text-sm text-slate-600">{label}</label>
      {children}
      {error && <span id={`${id}-err`} role="alert" className="text-xs text-red-600">{error}</span>}
    </div>
  );
}
