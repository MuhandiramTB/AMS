import { useEffect, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useAttendanceRecords, useCorrectAttendance } from '../api/hooks';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../api/client';
import {
  AsyncView, Button, Field, Input, PageHeader, StatusPill, Textarea,
  DataTable, Th, Td, Tr, Toolbar, Pagination, DEFAULT_PAGE_SIZE,
} from '../components/ui';
import { useDebounced } from '../lib/useDebounced';
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
  const [fromDate, setFromDate] = useState<string>('');
  const [toDate, setToDate] = useState<string>('');
  const debouncedEmp = useDebounced(employeeId);

  const filters = {
    ...(debouncedEmp ? { employeeId: Number(debouncedEmp) } : {}),
    ...(fromDate ? { fromDate } : {}),
    ...(toDate ? { toDate } : {}),
  };
  const records = useAttendanceRecords(page, DEFAULT_PAGE_SIZE, filters);
  const [editing, setEditing] = useState<AttendanceRecord | null>(null);

  const resetTo1 = () => setPage(1);

  return (
    <div className="space-y-5">
      <PageHeader
        title="Attendance"
        subtitle="Daily attendance records, exceptions, and corrections."
      />

      {/* Filter section */}
      <Toolbar>
        <Field id="empFilter" label="Employee ID" className="w-40">
          <Input
            id="empFilter"
            value={employeeId}
            onChange={(e) => { setEmployeeId(e.target.value); resetTo1(); }}
            placeholder="All employees"
            inputMode="numeric"
          />
        </Field>
        <Field id="fromDate" label="From" className="w-44">
          <Input id="fromDate" type="date" value={fromDate} onChange={(e) => { setFromDate(e.target.value); resetTo1(); }} />
        </Field>
        <Field id="toDate" label="To" className="w-44">
          <Input id="toDate" type="date" value={toDate} onChange={(e) => { setToDate(e.target.value); resetTo1(); }} />
        </Field>
        {(employeeId || fromDate || toDate) && (
          <Button onClick={() => { setEmployeeId(''); setFromDate(''); setToDate(''); resetTo1(); }}>Clear</Button>
        )}
        <div role="status" aria-live="polite" className="pb-2 text-xs text-[var(--color-muted-soft)]">
          {records.isFetching && records.isPlaceholderData ? 'Refreshing…' : ''}
        </div>
      </Toolbar>

      <AsyncView
        isLoading={records.isLoading}
        isError={records.isError}
        isEmpty={records.data?.items.length === 0}
        emptyText="No attendance records for this filter."
      >
        <DataTable
          className={records.isPlaceholderData ? 'opacity-60' : ''}
          head={
            <tr>
              <Th module="attendance">Emp</Th>
              <Th module="attendance">Date</Th>
              <Th module="attendance">In</Th>
              <Th module="attendance">Out</Th>
              <Th module="attendance">Worked</Th>
              <Th module="attendance" num>Late</Th>
              <Th module="attendance" num>OT</Th>
              <Th module="attendance">Status</Th>
              <Th module="attendance"><span className="sr-only">Actions</span></Th>
            </tr>
          }
        >
          {records.data?.items.map((r) => (
            <Tr key={r.id}>
              <Td>{r.employeeId}</Td>
              <Td>{r.workDate}</Td>
              <Td>{fmt(r.firstInUtc)}</Td>
              <Td>{fmt(r.lastOutUtc)}</Td>
              <Td>{mins(r.workedMinutes)}</Td>
              <Td num>{r.lateMinutes ? `${r.lateMinutes}m` : '—'}</Td>
              <Td num>{r.overtimeMinutes ? `${r.overtimeMinutes}m` : '—'}</Td>
              <Td>{statusPill(r.status)}</Td>
              <Td className="text-right">
                {canCorrect && <Button size="sm" onClick={() => setEditing(r)}>Review</Button>}
              </Td>
            </Tr>
          ))}
        </DataTable>
      </AsyncView>

      {records.data && (
        <Pagination page={records.data.page} totalPages={records.data.totalPages} onPage={setPage} />
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
  const dialogRef = useRef<HTMLDivElement>(null);
  const closeRef = useRef<HTMLButtonElement>(null);

  // Dialog focus management (WCAG 2.4.3 / 4.1.2): move focus into the dialog on
  // open, restore it to the element that opened the drawer on close, and trap Tab
  // within the dialog so focus never escapes to the obscured page behind it.
  useEffect(() => {
    const previouslyFocused = document.activeElement as HTMLElement | null;
    closeRef.current?.focus();

    function onKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') {
        e.preventDefault();
        onClose();
        return;
      }
      if (e.key !== 'Tab') return;
      const focusable = dialogRef.current?.querySelectorAll<HTMLElement>(
        'a[href], button:not([disabled]), input, textarea, select, [tabindex]:not([tabindex="-1"])',
      );
      if (!focusable || focusable.length === 0) return;
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    }

    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      previouslyFocused?.focus();
    };
  }, [onClose]);

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
    <div
      ref={dialogRef}
      role="dialog"
      aria-modal="true"
      aria-labelledby="corr-title"
      className="fixed inset-y-0 right-0 z-10 flex w-full max-w-md flex-col overflow-auto border-l border-[var(--color-line)] bg-[var(--color-surface)] p-6 shadow-[var(--shadow-pop)]"
    >
      <div className="mb-5 flex items-start justify-between gap-4">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-[var(--color-muted)]">Correction</p>
          <h2 id="corr-title" className="mt-0.5 text-lg font-bold tracking-tight text-[var(--color-ink)]">
            Record — Emp {record.employeeId}, {record.workDate}
          </h2>
        </div>
        <button
          ref={closeRef}
          onClick={onClose}
          className="rounded-[var(--radius-md)] p-1 text-[var(--color-muted-soft)] transition-colors hover:bg-[var(--color-surface-3)] hover:text-[var(--color-ink)]"
          aria-label="Close"
        >
          ✕
        </button>
      </div>

      <div className="mb-5 rounded-[var(--radius-md)] border border-[var(--color-line-soft)] bg-[var(--color-surface-2)] p-4 text-sm">
        <div className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-[var(--color-muted)]">Computed</div>
        <div className="text-[var(--color-ink-soft)]">Worked: {mins(record.workedMinutes)} · Late: {record.lateMinutes}m · OT: {record.overtimeMinutes}m</div>
        <div className="mt-2">{statusPill(record.status)}</div>
        {record.exceptions.length > 0 && (
          <ul className="mt-2 list-disc pl-5 text-[var(--color-late)]">
            {record.exceptions.map((ex) => <li key={ex.id}>{ex.type}{ex.isResolved ? ' (resolved)' : ''}</li>)}
          </ul>
        )}
      </div>

      <form onSubmit={onSubmit} className="space-y-4">
        <Field id="firstIn" label="First in">
          <input
            id="firstIn"
            type="datetime-local"
            className="w-full rounded-[var(--radius-md)] border border-[var(--color-line)] bg-white px-3 py-2 text-sm text-[var(--color-ink)] transition-colors focus:border-[var(--color-brand-600)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-600)]/20"
            {...register('firstInUtc')}
          />
        </Field>
        <Field id="lastOut" label="Last out">
          <input
            id="lastOut"
            type="datetime-local"
            className="w-full rounded-[var(--radius-md)] border border-[var(--color-line)] bg-white px-3 py-2 text-sm text-[var(--color-ink)] transition-colors focus:border-[var(--color-brand-600)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-600)]/20"
            {...register('lastOutUtc')}
          />
        </Field>
        <div>
          <label className="mb-1 block text-sm font-medium text-[var(--color-ink-soft)]" htmlFor="reason">Reason *</label>
          <Textarea
            id="reason"
            rows={3}
            {...register('reason', { required: 'A reason is required for every correction.' })}
          />
          {formState.errors.reason && (
            <span role="alert" className="mt-1 block text-xs font-medium text-[var(--color-danger)]">{formState.errors.reason.message}</span>
          )}
        </div>

        {error && <p role="alert" className="rounded-[var(--radius-md)] bg-[var(--color-absent-bg)] px-3 py-2 text-sm font-medium text-[var(--color-danger)]">{error}</p>}

        <div className="flex gap-2 pt-1">
          <Button type="submit" variant="primary" loading={correct.isPending} disabled={correct.isPending}>
            {correct.isPending ? 'Saving…' : 'Save & recalculate'}
          </Button>
          <Button onClick={onClose}>Cancel</Button>
        </div>
        <p className="text-xs text-[var(--color-muted-soft)]">
          Raw device punches are immutable; corrections adjust the record only and are fully audited.
        </p>
      </form>
    </div>
  );
}
