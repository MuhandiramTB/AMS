import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useAssignShift, useCreateShift, useShifts, type CreateShiftInput } from '../api/hooks';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../api/client';
import { AsyncView, Button, StatusPill } from '../components/ui';

export function ShiftsPage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission('Shift.Write');
  const shifts = useShifts();
  const [message, setMessage] = useState<string | null>(null);

  return (
    <div>
      <h1 className="mb-4 text-2xl font-semibold text-slate-800">Shifts</h1>

      {message && <p className="mb-4 rounded bg-slate-100 px-3 py-2 text-sm text-slate-700" role="status">{message}</p>}

      {canWrite && (
        <div className="mb-6 grid gap-6 lg:grid-cols-2">
          <CreateShiftForm onDone={setMessage} />
          <AssignShiftForm onDone={setMessage} />
        </div>
      )}

      <AsyncView
        isLoading={shifts.isLoading}
        isError={shifts.isError}
        isEmpty={shifts.data?.length === 0}
        emptyText="No shifts defined yet."
      >
        <table className="w-full border-collapse text-left text-sm">
          <thead>
            <tr className="border-b border-slate-200 text-slate-500">
              <th className="py-2">Code</th><th className="py-2">Name</th><th className="py-2">Window</th>
              <th className="py-2">Break</th><th className="py-2">Grace</th><th className="py-2">Type</th>
            </tr>
          </thead>
          <tbody>
            {shifts.data?.map((s) => (
              <tr key={s.id} className="border-b border-slate-100">
                <td className="py-2 font-mono">{s.code}</td>
                <td className="py-2">{s.name}</td>
                <td className="py-2">{s.startTime.slice(0, 5)}–{s.endTime.slice(0, 5)}</td>
                <td className="py-2">{s.breakMinutes}m</td>
                <td className="py-2">{s.graceInMinutes}/{s.graceOutMinutes}m</td>
                <td className="py-2">
                  {s.isOvernight
                    ? <StatusPill tone="info" label="Overnight" />
                    : <StatusPill tone="neutral" label="Day" />}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </AsyncView>
    </div>
  );
}

function CreateShiftForm({ onDone }: { onDone: (m: string) => void }) {
  const create = useCreateShift();
  const { register, handleSubmit, reset } = useForm<CreateShiftInput>({
    defaultValues: { breakMinutes: 60, graceInMinutes: 10, graceOutMinutes: 10, overtimeThresholdMinutes: 0 },
  });
  const [err, setErr] = useState<string | null>(null);

  const onSubmit = handleSubmit(async (v) => {
    setErr(null);
    try {
      await create.mutateAsync({
        ...v,
        startTime: `${v.startTime}:00`,
        endTime: `${v.endTime}:00`,
        breakMinutes: Number(v.breakMinutes),
        graceInMinutes: Number(v.graceInMinutes),
        graceOutMinutes: Number(v.graceOutMinutes),
        overtimeThresholdMinutes: Number(v.overtimeThresholdMinutes),
      });
      reset();
      onDone('Shift created.');
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : 'Failed to create shift.');
    }
  });

  return (
    <form onSubmit={onSubmit} className="rounded border border-slate-200 p-4">
      <h2 className="mb-3 font-medium text-slate-800">New shift</h2>
      <p className="mb-2 text-xs text-slate-500">Set end earlier than start for an overnight shift.</p>
      <div className="grid grid-cols-2 gap-2">
        <input placeholder="Code" className="rounded border border-slate-300 px-2 py-1" {...register('code', { required: true })} />
        <input placeholder="Name" className="rounded border border-slate-300 px-2 py-1" {...register('name', { required: true })} />
        <label className="text-xs text-slate-600">Start<input type="time" className="w-full rounded border border-slate-300 px-2 py-1" {...register('startTime', { required: true })} /></label>
        <label className="text-xs text-slate-600">End<input type="time" className="w-full rounded border border-slate-300 px-2 py-1" {...register('endTime', { required: true })} /></label>
        <label className="text-xs text-slate-600">Break (m)<input type="number" className="w-full rounded border border-slate-300 px-2 py-1" {...register('breakMinutes')} /></label>
        <label className="text-xs text-slate-600">OT threshold (m)<input type="number" className="w-full rounded border border-slate-300 px-2 py-1" {...register('overtimeThresholdMinutes')} /></label>
        <label className="text-xs text-slate-600">Grace in (m)<input type="number" className="w-full rounded border border-slate-300 px-2 py-1" {...register('graceInMinutes')} /></label>
        <label className="text-xs text-slate-600">Grace out (m)<input type="number" className="w-full rounded border border-slate-300 px-2 py-1" {...register('graceOutMinutes')} /></label>
      </div>
      <div className="mt-3">
        <Button type="submit" variant="primary" disabled={create.isPending}>Create shift</Button>
        {err && <span role="alert" className="ml-2 text-sm text-red-600">{err}</span>}
      </div>
    </form>
  );
}

interface AssignForm {
  shiftId: string;
  target: 'employee' | 'department';
  targetId: string;
  effectiveFrom: string;
}

function AssignShiftForm({ onDone }: { onDone: (m: string) => void }) {
  const assign = useAssignShift();
  const shifts = useShifts();
  const { register, handleSubmit, reset, watch } = useForm<AssignForm>({ defaultValues: { target: 'employee' } });
  const [err, setErr] = useState<string | null>(null);
  const target = watch('target');

  const onSubmit = handleSubmit(async (v) => {
    setErr(null);
    try {
      await assign.mutateAsync({
        shiftId: Number(v.shiftId),
        employeeId: v.target === 'employee' ? Number(v.targetId) : null,
        departmentId: v.target === 'department' ? Number(v.targetId) : null,
        effectiveFrom: v.effectiveFrom,
      });
      reset({ target: 'employee' });
      onDone('Shift assigned.');
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : 'Failed to assign shift.');
    }
  });

  return (
    <form onSubmit={onSubmit} className="rounded border border-slate-200 p-4">
      <h2 className="mb-3 font-medium text-slate-800">Assign shift (effective-dated)</h2>
      <div className="grid grid-cols-2 gap-2">
        <select className="rounded border border-slate-300 px-2 py-1" {...register('shiftId', { required: true })}>
          <option value="">Shift…</option>
          {shifts.data?.map((s) => <option key={s.id} value={s.id}>{s.code} — {s.name}</option>)}
        </select>
        <select className="rounded border border-slate-300 px-2 py-1" {...register('target')}>
          <option value="employee">Employee</option>
          <option value="department">Department</option>
        </select>
        <input placeholder={target === 'employee' ? 'Employee ID' : 'Department ID'} type="number" className="rounded border border-slate-300 px-2 py-1" {...register('targetId', { required: true })} />
        <label className="text-xs text-slate-600">Effective from<input type="date" className="w-full rounded border border-slate-300 px-2 py-1" {...register('effectiveFrom', { required: true })} /></label>
      </div>
      <div className="mt-3">
        <Button type="submit" variant="primary" disabled={assign.isPending}>Assign</Button>
        {err && <span role="alert" className="ml-2 text-sm text-red-600">{err}</span>}
      </div>
    </form>
  );
}
