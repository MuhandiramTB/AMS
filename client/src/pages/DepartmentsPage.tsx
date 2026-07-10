import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useCreateDepartment, useDepartments } from '../api/hooks';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../api/client';

function messageFor(error: unknown): string {
  return error instanceof ApiError ? error.message : 'An unexpected error occurred.';
}

interface DeptForm {
  code: string;
  name: string;
}

export function DepartmentsPage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission('Department.Write');
  const { data, isLoading, isError } = useDepartments();
  const createDepartment = useCreateDepartment();
  const { register, handleSubmit, reset } = useForm<DeptForm>();
  const [formError, setFormError] = useState<string | null>(null);

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await createDepartment.mutateAsync(values);
      reset();
    } catch (error) {
      setFormError(messageFor(error));
    }
  });

  return (
    <div>
      <h1 className="mb-4 text-2xl font-semibold text-slate-800">Departments</h1>

      {canWrite && (
        <form onSubmit={onSubmit} className="mb-6 flex flex-wrap items-end gap-3">
          <div>
            <label className="block text-sm text-slate-600" htmlFor="code">Code</label>
            <input id="code" className="rounded border border-slate-300 px-2 py-1" {...register('code', { required: true })} />
          </div>
          <div>
            <label className="block text-sm text-slate-600" htmlFor="name">Name</label>
            <input id="name" className="rounded border border-slate-300 px-2 py-1" {...register('name', { required: true })} />
          </div>
          <button
            type="submit"
            disabled={createDepartment.isPending}
            className="rounded bg-blue-600 px-4 py-1.5 text-white hover:bg-blue-700 disabled:opacity-60"
          >
            {createDepartment.isPending ? 'Adding…' : 'Add department'}
          </button>
          {formError && <span role="alert" className="text-sm text-red-600">{formError}</span>}
        </form>
      )}

      {isLoading && <p className="text-slate-500">Loading…</p>}
      {isError && <p className="text-red-600">Failed to load departments.</p>}

      {data && (
        <table className="w-full border-collapse text-left text-sm">
          <thead>
            <tr className="border-b border-slate-200 text-slate-500">
              <th scope="col" className="py-2">Code</th>
              <th scope="col" className="py-2">Name</th>
              <th scope="col" className="py-2">Status</th>
            </tr>
          </thead>
          <tbody>
            {data.length === 0 && (
              <tr><td colSpan={3} className="py-4 text-slate-400">No departments yet.</td></tr>
            )}
            {data.map((d) => (
              <tr key={d.id} className="border-b border-slate-100">
                <td className="py-2 font-mono">{d.code}</td>
                <td className="py-2">{d.name}</td>
                <td className="py-2">
                  <span className={d.isActive ? 'text-green-700' : 'text-slate-400'}>
                    {d.isActive ? '● Active' : '○ Inactive'}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
