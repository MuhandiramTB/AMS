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
import {
  AsyncView,
  Button,
  Card,
  Field,
  Input,
  PageHeader,
  Select,
  StatusPill,
  DataTable,
  Td,
  Th,
  Tr,
  Toolbar,
  Pagination,
  DEFAULT_PAGE_SIZE,
} from '../components/ui';
import { useDebounced } from '../lib/useDebounced';
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
  const [status, setStatus] = useState('');
  const debouncedEmp = useDebounced(employeeId);
  const filters = {
    ...(debouncedEmp ? { employeeId: Number(debouncedEmp) } : {}),
    ...(status ? { status } : {}),
  };
  const requests = useLeaveRequests(page, DEFAULT_PAGE_SIZE, filters);
  const [message, setMessage] = useState<string | null>(null);

  return (
    <div className="space-y-5">
      <PageHeader title="Leave" subtitle="Request time off and manage leave requests." />

      {message && (
        <p
          className="rounded-[var(--radius-md)] bg-[var(--color-surface-2)] px-4 py-3 text-sm text-[var(--color-ink-soft)]"
          role="status"
        >
          {message}
        </p>
      )}

      {canRequest && <RequestLeaveForm onDone={setMessage} />}

      {/* Filter section */}
      <Toolbar>
        <Field id="empFilter" label="Filter by Employee ID" className="w-48">
          <Input
            id="empFilter"
            value={employeeId}
            onChange={(e) => { setEmployeeId(e.target.value); setPage(1); }}
            placeholder="All employees"
            inputMode="numeric"
          />
        </Field>
        <Field id="statusFilter" label="Status" className="w-44">
          <Select id="statusFilter" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }}>
            <option value="">All statuses</option>
            <option value="Submitted">Submitted</option>
            <option value="Approved">Approved</option>
            <option value="Applied">Applied</option>
            <option value="Rejected">Rejected</option>
            <option value="Cancelled">Cancelled</option>
          </Select>
        </Field>
        {(employeeId || status) && (
          <Button onClick={() => { setEmployeeId(''); setStatus(''); setPage(1); }}>Clear</Button>
        )}
        {employeeId && <div className="flex-1 pb-1"><BalancesPanel employeeId={Number(employeeId)} /></div>}
      </Toolbar>

      <AsyncView
        isLoading={requests.isLoading}
        isError={requests.isError}
        error={requests.error}
        isEmpty={requests.data?.items.length === 0}
        emptyText="No leave requests for this filter."
      >
        <DataTable
          head={
            <tr>
              <Th module="leave">Emp</Th>
              <Th module="leave">Type</Th>
              <Th module="leave">Dates</Th>
              <Th module="leave" num>Days</Th>
              <Th module="leave">Status</Th>
              <Th module="leave">Actions</Th>
            </tr>
          }
        >
          {requests.data?.items.map((r) => (
            <LeaveRow key={r.id} req={r} canApprove={canApprove} canRequest={canRequest} onMessage={setMessage} />
          ))}
        </DataTable>
      </AsyncView>

      {requests.data && (
        <Pagination page={requests.data.page} totalPages={requests.data.totalPages} onPage={setPage} />
      )}
    </div>
  );
}

function LeaveRow({
  req,
  canApprove,
  canRequest,
  onMessage,
}: {
  req: LeaveRequest;
  canApprove: boolean;
  canRequest: boolean;
  onMessage: (m: string) => void;
}) {
  const approve = useApproveLeave();
  const reject = useRejectLeave();
  const cancel = useCancelLeave();

  const run = async (fn: () => Promise<unknown>, ok: string) => {
    try {
      await fn();
      onMessage(ok);
    } catch (e) {
      onMessage(e instanceof ApiError ? e.message : 'Action failed.');
    }
  };

  const pending = req.status === 'Submitted';
  const active = req.status === 'Submitted' || req.status === 'Approved' || req.status === 'Applied';

  return (
    <Tr>
      <Td>{req.employeeId}</Td>
      <Td>{req.leaveTypeId}</Td>
      <Td>
        {req.startDate} → {req.endDate}
      </Td>
      <Td num>{req.dayCount}</Td>
      <Td>{statusPill(req.status)}</Td>
      <Td>
        <div className="flex flex-wrap gap-2">
          {canApprove && pending && (
            <>
              <Button
                size="sm"
                variant="primary"
                loading={approve.isPending}
                onClick={() => run(() => approve.mutateAsync(req.id), 'Leave approved.')}
              >
                Approve
              </Button>
              <Button
                size="sm"
                variant="danger"
                loading={reject.isPending}
                onClick={() => run(() => reject.mutateAsync(req.id), 'Leave rejected.')}
              >
                Reject
              </Button>
            </>
          )}
          {canRequest && active && (
            <Button
              size="sm"
              loading={cancel.isPending}
              onClick={() => run(() => cancel.mutateAsync(req.id), 'Leave cancelled.')}
            >
              Cancel
            </Button>
          )}
        </div>
      </Td>
    </Tr>
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
    <Card className="mb-6" pad>
      <form onSubmit={onSubmit}>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <Field id="lvEmp" label="Employee ID">
            <Input id="lvEmp" type="number" {...register('employeeId', { required: true })} />
          </Field>
          <Field id="lvType" label="Type">
            <Select id="lvType" {...register('leaveTypeId', { required: true })}>
              <option value="">Type…</option>
              {types.data?.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field id="lvFrom" label="From">
            <Input id="lvFrom" type="date" {...register('startDate', { required: true })} />
          </Field>
          <Field id="lvTo" label="To">
            <Input id="lvTo" type="date" {...register('endDate', { required: true })} />
          </Field>
          <Field id="lvReason" label="Reason">
            <Input id="lvReason" {...register('reason')} />
          </Field>
        </div>

        <div className="mt-4 flex flex-wrap items-center gap-3">
          <Button type="submit" variant="primary" loading={request.isPending}>
            Request leave
          </Button>
          {err && (
            <span role="alert" className="text-sm font-medium text-[var(--color-danger)]">
              {err}
            </span>
          )}
        </div>
      </form>
    </Card>
  );
}

function BalancesPanel({ employeeId }: { employeeId: number }) {
  const year = new Date().getFullYear();
  const balances = useLeaveBalances(employeeId, year);
  if (!balances.data || balances.data.length === 0) return null;
  return (
    <div className="text-sm text-[var(--color-ink-soft)]">
      <span className="font-semibold text-[var(--color-ink)]">Balances {year}:</span>{' '}
      {balances.data.map((b) => (
        <span key={b.id} className="ml-2 text-[var(--color-muted)]">
          type {b.leaveTypeId}: <strong className="text-[var(--color-ink)]">{b.remainingDays}</strong>/{b.entitledDays}
        </span>
      ))}
    </div>
  );
}
