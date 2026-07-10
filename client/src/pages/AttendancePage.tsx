import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useAttendanceRecords, useCorrectAttendance } from '../api/hooks';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../api/client';
import { AsyncView, Button, StatusPill } from '../components/ui';
import type { AttendanceRecord } from '../api/types';

function statusPill(status: string) {
  switch (status) {
    case 'Processed':
    case 'Finalized':
      return <StatusPill tone="success" label={status} />;
    case 'Exception':
      return <StatusPill tone="warning" label="Exception" />;
    case 'UnderReview':
      return <StatusPill tone="info" label="Under review" />;
    case 'Corrected':
      return <StatusPill tone="info" label="Corrected" />;
    default:
      return <StatusPill tone="neutral" label={status} />;
  }
}

function fmt(iso: string | null): string {
  return iso ? new Date(iso).toLocaleString() : '—';
}

function mins(m: number | null): string {
  if (m === null) return '—';
  return `${Math.floor(m / 60)}h ${m % 60}m`;
}

export function AttendancePage() {
  const { hasPermission } = useAuth();
  const canCorrect = hasPermission('Attendance.Correct');
  const [page, setPage] = useState(1);
  const [employeeId, setEmployeeId] = useState<string>('');
  const filters = employeeId ? { employeeId: Number(employeeId) } : {};
  const records = useAttendanceRecords(page, 15, filters);
  const [editing, setEditing] = useState<AttendanceRecord | null>(null);

  return (
    <div>
      <h1 className="mb-4 text-2xl font-semibold text-slate-800">Attendance</h1>

      <div className="mb-4 flex items-end gap-2">
        <div>
          <label className="block text-sm text-slate-600" htmlFor="empFilter">Employee ID</label>
          <input
            id="empFilter"
            className="rounded border border-slate-300 px-2 py-1"
            value={employeeId}
            onChange={(e) => { setEmployeeId(e.target.value); setPage(1); }}
            placeholder="all"
          />
        </div>
      </div>

      <AsyncView
        isLoading={records.isLoading}
        isError={records.isError}
        isEmpty={records.data?.items.length === 0}
        emptyText="No attendance records for this filter."
      >
        <table className={`w-full border-collapse text-left text-sm ${records.isPlaceholderData ? 'opacity-60' : ''}`}>
          <thead>
            <tr className="border-b border-slate-200 text-slate-500">
              <th className="py-2">Emp</th><th className="py-2">Date</th><th className="py-2">In</th>
              <th className="py-2">Out</th><th className="py-2">Worked</th><th className="py-2">Late</th>
              <th className="py-2">OT</th><th className="py-2">Status</th><th className="py-2"></th>
            </tr>
          </thead>
          <tbody>
            {records.data?.items.map((r) => (
              <tr key={r.id} className="border-b border-slate-100">
                <td className="py-2">{r.employeeId}</td>
                <td className="py-2">{r.workDate}</td>
                <td className="py-2">{fmt(r.firstInUtc)}</td>
                <td className="py-2">{fmt(r.lastOutUtc)}</td>
                <td className="py-2">{mins(r.workedMinutes)}</td>
                <td className="py-2">{r.lateMinutes ? `${r.lateMinutes}m` : '—'}</td>
                <td className="py-2">{r.overtimeMinutes ? `${r.overtimeMinutes}m` : '—'}</td>
                <td className="py-2">{statusPill(r.status)}</td>
                <td className="py-2">
                  {canCorrect && <Button onClick={() => setEditing(r)}>Review</Button>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </AsyncView>

      {records.data && (
        <div className="mt-4 flex items-center gap-3 text-sm">
          <Button disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Prev</Button>
          <span className="text-slate-500">Page {records.data.page} of {records.data.totalPages || 1}</span>
          <Button disabled={page >= records.data.totalPages} onClick={() => setPage((p) => p + 1)}>Next</Button>
        </div>
      )}

      {editing && <CorrectionDrawer record={editing} onClose={() => setEditing(null)} />}
    </div>
  );
}

function toLocalInput(iso: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  // datetime-local wants "yyyy-MM-ddTHH:mm"
  const pad = (n: number) => n.toString().padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

interface CorrectionForm {
  firstInUtc: string;
  lastOutUtc: string;
  reason: string;
}

function CorrectionDrawer({ record, onClose }: { record: AttendanceRecord; onClose: () => void }) {
  const correct = useCorrectAttendance();
  const { register, handleSubmit, formState } = useForm<CorrectionForm>({
    defaultValues: {
      firstInUtc: toLocalInput(record.firstInUtc),
      lastOutUtc: toLocalInput(record.lastOutUtc),
      reason: '',
    },
  });
  const [error, setError] = useState<string | null>(null);

  const onSubmit = handleSubmit(async (values) => {
    setError(null);
    try {
      await correct.mutateAsync({
        id: record.id,
        firstInUtc: values.firstInUtc ? new Date(values.firstInUtc).toISOString() : null,
        lastOutUtc: values.lastOutUtc ? new Date(values.lastOutUtc).toISOString() : null,
        reason: values.reason,
        ifMatch: record.concurrencyToken,
      });
      onClose();
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) {
        setError('This record changed since you opened it. Close and reopen to see the latest, then retry.');
      } else if (e instanceof ApiError) {
        setError(e.message);
      } else {
        setError('Correction failed.');
      }
    }
  });

  return (
    <div className="fixed inset-y-0 right-0 z-10 w-full max-w-md overflow-auto border-l border-slate-200 bg-white p-6 shadow-xl">
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-lg font-semibold text-slate-800">
          Record — Emp {record.employeeId}, {record.workDate}
        </h2>
        <button onClick={onClose} className="text-slate-400 hover:text-slate-700" aria-label="Close">✕</button>
      </div>

      <div className="mb-4 rounded bg-slate-50 p-3 text-sm">
        <div className="mb-1 font-medium text-slate-600">Computed</div>
        <div>Worked: {mins(record.workedMinutes)} · Late: {record.lateMinutes}m · OT: {record.overtimeMinutes}m</div>
        <div className="mt-1">{statusPill(record.status)}</div>
        {record.exceptions.length > 0 && (
          <ul className="mt-2 list-disc pl-5 text-amber-700">
            {record.exceptions.map((ex) => <li key={ex.id}>{ex.type}{ex.isResolved ? ' (resolved)' : ''}</li>)}
          </ul>
        )}
      </div>

      <form onSubmit={onSubmit} className="space-y-3">
        <div>
          <label className="block text-sm text-slate-600" htmlFor="firstIn">First in</label>
          <input id="firstIn" type="datetime-local" className="w-full rounded border border-slate-300 px-2 py-1" {...register('firstInUtc')} />
        </div>
        <div>
          <label className="block text-sm text-slate-600" htmlFor="lastOut">Last out</label>
          <input id="lastOut" type="datetime-local" className="w-full rounded border border-slate-300 px-2 py-1" {...register('lastOutUtc')} />
        </div>
        <div>
          <label className="block text-sm text-slate-600" htmlFor="reason">Reason *</label>
          <textarea
            id="reason"
            className="w-full rounded border border-slate-300 px-2 py-1"
            rows={3}
            {...register('reason', { required: 'A reason is required for every correction.' })}
          />
          {formState.errors.reason && (
            <span role="alert" className="text-xs text-red-600">{formState.errors.reason.message}</span>
          )}
        </div>

        {error && <p role="alert" className="text-sm text-red-600">{error}</p>}

        <div className="flex gap-2">
          <Button type="submit" variant="primary" disabled={correct.isPending}>
            {correct.isPending ? 'Saving…' : 'Save & recalculate'}
          </Button>
          <Button onClick={onClose}>Cancel</Button>
        </div>
        <p className="text-xs text-slate-400">
          Raw device punches are immutable; corrections adjust the record only and are fully audited.
        </p>
      </form>
    </div>
  );
}
