import { useState } from 'react';
import { useForm } from 'react-hook-form';
import {
  useApproveLeave,
  useCancelLeave,
  useLeaveBalances,
  useLeaveRequests,
  useLeaveTypes,
  useRejectLeave,
  useRequestLeave,
  type RequestLeaveInput,
} from '../api/hooks';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../api/client';
import { AsyncView, Button, StatusPill } from '../components/ui';
import type { LeaveRequest } from '../api/types';

function statusPill(status: string) {
  switch (status) {
    case 'Approved':
    case 'Applied':
      return <StatusPill tone="success" label={status} />;
    case 'Submitted':
      return <StatusPill tone="info" label="Submitted" />;
    case 'Rejected':
      return <StatusPill tone="danger" label="Rejected" />;
    case 'Cancelled':
      return <StatusPill tone="neutral" label="Cancelled" />;
    default:
      return <StatusPill tone="neutral" label={status} />;
  }
}

export function LeavePage() {
  const { hasPermission } = useAuth();
  const canRequest = hasPermission('Leave.Request');
  const canApprove = hasPermission('Leave.Approve');

  const [page, setPage] = useState(1);
  const [employeeId, setEmployeeId] = useState('');
  const filters = employeeId ? { employeeId: Number(employeeId) } : {};
  const requests = useLeaveRequests(page, 15, filters);
  const [message, setMessage] = useState<string | null>(null);

  return (
    <div>
      <h1 className="mb-4 text-2xl font-semibold text-slate-800">Leave</h1>
      {message && <p className="mb-4 rounded bg-slate-100 px-3 py-2 text-sm text-slate-700" role="status">{message}</p>}

      {canRequest && <RequestLeaveForm onDone={setMessage} />}

      <div className="mb-4 flex items-end gap-2">
        <div>
          <label className="block text-sm text-slate-600" htmlFor="empFilter">Filter by Employee ID</label>
          <input id="empFilter" className="rounded border border-slate-300 px-2 py-1"
                 value={employeeId} onChange={(e) => { setEmployeeId(e.target.value); setPage(1); }} placeholder="all" />
        </div>
        {employeeId && <BalancesPanel employeeId={Number(employeeId)} />}
      </div>

      <AsyncView
        isLoading={requests.isLoading}
        isError={requests.isError}
        isEmpty={requests.data?.items.length === 0}
        emptyText="No leave requests for this filter."
      >
        <table className="w-full border-collapse text-left text-sm">
          <thead>
            <tr className="border-b border-slate-200 text-slate-500">
              <th className="py-2">Emp</th><th className="py-2">Type</th><th className="py-2">Dates</th>
              <th className="py-2">Days</th><th className="py-2">Status</th><th className="py-2">Actions</th>
            </tr>
          </thead>
          <tbody>
            {requests.data?.items.map((r) => (
              <LeaveRow key={r.id} req={r} canApprove={canApprove} canRequest={canRequest} onMessage={setMessage} />
            ))}
          </tbody>
        </table>
      </AsyncView>

      {requests.data && (
        <div className="mt-4 flex items-center gap-3 text-sm">
          <Button disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Prev</Button>
          <span className="text-slate-500">Page {requests.data.page} of {requests.data.totalPages || 1}</span>
          <Button disabled={page >= requests.data.totalPages} onClick={() => setPage((p) => p + 1)}>Next</Button>
        </div>
      )}
    </div>
  );
}

function LeaveRow({
  req, canApprove, canRequest, onMessage,
}: { req: LeaveRequest; canApprove: boolean; canRequest: boolean; onMessage: (m: string) => void }) {
  const approve = useApproveLeave();
  const reject = useRejectLeave();
  const cancel = useCancelLeave();

  const run = async (fn: () => Promise<unknown>, ok: string) => {
    try { await fn(); onMessage(ok); }
    catch (e) { onMessage(e instanceof ApiError ? e.message : 'Action failed.'); }
  };

  const pending = req.status === 'Submitted';
  const active = req.status === 'Submitted' || req.status === 'Approved' || req.status === 'Applied';

  return (
    <tr className="border-b border-slate-100">
      <td className="py-2">{req.employeeId}</td>
      <td className="py-2">{req.leaveTypeId}</td>
      <td className="py-2">{req.startDate} → {req.endDate}</td>
      <td className="py-2">{req.dayCount}</td>
      <td className="py-2">{statusPill(req.status)}</td>
      <td className="py-2">
        <div className="flex flex-wrap gap-1">
          {canApprove && pending && (
            <>
              <Button variant="primary" onClick={() => run(() => approve.mutateAsync(req.id), 'Leave approved.')}>Approve</Button>
              <Button variant="danger" onClick={() => run(() => reject.mutateAsync(req.id), 'Leave rejected.')}>Reject</Button>
            </>
          )}
          {canRequest && active && (
            <Button onClick={() => run(() => cancel.mutateAsync(req.id), 'Leave cancelled.')}>Cancel</Button>
          )}
        </div>
      </td>
    </tr>
  );
}

function RequestLeaveForm({ onDone }: { onDone: (m: string) => void }) {
  const types = useLeaveTypes();
  const request = useRequestLeave();
  const { register, handleSubmit, reset } = useForm<RequestLeaveInput>();
  const [err, setErr] = useState<string | null>(null);

  const onSubmit = handleSubmit(async (v) => {
    setErr(null);
    try {
      await request.mutateAsync({ ...v, employeeId: Number(v.employeeId), leaveTypeId: Number(v.leaveTypeId) });
      reset();
      onDone('Leave request submitted.');
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : 'Failed to submit request.');
    }
  });

  return (
    <form onSubmit={onSubmit} className="mb-6 flex flex-wrap items-end gap-2 rounded border border-slate-200 p-4">
      <div>
        <label className="block text-xs text-slate-600" htmlFor="lvEmp">Employee ID</label>
        <input id="lvEmp" type="number" className="rounded border border-slate-300 px-2 py-1" {...register('employeeId', { required: true })} />
      </div>
      <div>
        <label className="block text-xs text-slate-600" htmlFor="lvType">Type</label>
        <select id="lvType" className="rounded border border-slate-300 px-2 py-1" {...register('leaveTypeId', { required: true })}>
          <option value="">Type…</option>
          {types.data?.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
        </select>
      </div>
      <div>
        <label className="block text-xs text-slate-600" htmlFor="lvFrom">From</label>
        <input id="lvFrom" type="date" className="rounded border border-slate-300 px-2 py-1" {...register('startDate', { required: true })} />
      </div>
      <div>
        <label className="block text-xs text-slate-600" htmlFor="lvTo">To</label>
        <input id="lvTo" type="date" className="rounded border border-slate-300 px-2 py-1" {...register('endDate', { required: true })} />
      </div>
      <div>
        <label className="block text-xs text-slate-600" htmlFor="lvReason">Reason</label>
        <input id="lvReason" className="rounded border border-slate-300 px-2 py-1" {...register('reason')} />
      </div>
      <Button type="submit" variant="primary" disabled={request.isPending}>Request leave</Button>
      {err && <span role="alert" className="text-sm text-red-600">{err}</span>}
    </form>
  );
}

function BalancesPanel({ employeeId }: { employeeId: number }) {
  const year = new Date().getFullYear();
  const balances = useLeaveBalances(employeeId, year);
  if (!balances.data || balances.data.length === 0) return null;
  return (
    <div className="text-sm text-slate-600">
      <span className="font-medium">Balances {year}:</span>{' '}
      {balances.data.map((b) => (
        <span key={b.id} className="ml-2">
          type {b.leaveTypeId}: <strong>{b.remainingDays}</strong>/{b.entitledDays}
        </span>
      ))}
    </div>
  );
}
