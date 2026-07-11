import { useState } from 'react';
import { useForm } from 'react-hook-form';
import {
  useAssignShift,
  useCreateShift,
  useSetShiftActive,
  useShifts,
  useUpdateShift,
  type CreateShiftInput,
} from '../api/hooks';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../api/client';
import type { Shift } from '../api/types';
import {
  AsyncView,
  Button,
  Card,
  ConfirmDialog,
  DataTable,
  Field,
  Input,
  Modal,
  PageHeader,
  RowActions,
  SearchInput,
  Select,
  StatusPill,
  Td,
  Th,
  Toolbar,
  Tr,
  useToast,
} from '../components/ui';

export function ShiftsPage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission('Shift.Write');
  const shifts = useShifts();
  const setActive = useSetShiftActive();
  const toast = useToast();
  const [message, setMessage] = useState<string | null>(null);
  const [query, setQuery] = useState('');
  const [showForm, setShowForm] = useState(true);
  const [editing, setEditing] = useState<Shift | null>(null);
  const [confirming, setConfirming] = useState<Shift | null>(null);

  const q = query.trim().toLowerCase();
  const filtered = q
    ? shifts.data?.filter((s) => s.code.toLowerCase().includes(q) || s.name.toLowerCase().includes(q))
    : shifts.data;

  const onConfirmActive = async () => {
    if (!confirming) return;
    const target = confirming;
    const activate = !target.isActive;
    try {
      await setActive.mutateAsync({ id: target.id, active: activate });
      setConfirming(null);
      toast(activate ? 'Shift activated.' : 'Shift deactivated.', 'success');
    } catch (e) {
      toast(e instanceof ApiError ? e.message : 'Failed to update shift status.', 'error');
    }
  };

  return (
    <div>
      <PageHeader
        title="Shifts"
        subtitle="Define shift windows and assign them to employees or departments."
        actions={
          canWrite ? (
            <Button variant="secondary" onClick={() => setShowForm((v) => !v)}>
              {showForm ? 'Hide forms' : 'New shift'}
            </Button>
          ) : undefined
        }
      />

      {message && (
        <p className="mb-6 rounded-[var(--radius-md)] bg-[var(--color-surface-2)] px-4 py-3 text-sm text-[var(--color-ink-soft)]" role="status">
          {message}
        </p>
      )}

      {canWrite && showForm && (
        <div className="mb-6 grid gap-6 lg:grid-cols-2">
          <CreateShiftForm onDone={setMessage} />
          <AssignShiftForm onDone={setMessage} />
        </div>
      )}

      <Toolbar>
        <SearchInput
          value={query}
          onChange={setQuery}
          label="Search shifts"
          placeholder="Code or name…"
        />
      </Toolbar>

      <AsyncView
        isLoading={shifts.isLoading}
        isError={shifts.isError}
        isEmpty={shifts.data?.length === 0}
        emptyText="No shifts defined yet."
      >
        <DataTable
          head={
            <tr>
              <Th module="shifts">Code</Th>
              <Th module="shifts">Name</Th>
              <Th module="shifts">Window</Th>
              <Th module="shifts">Break</Th>
              <Th module="shifts">Grace</Th>
              <Th module="shifts">Type</Th>
              <Th module="shifts">Status</Th>
              {canWrite && <Th module="shifts"><span className="sr-only">Actions</span></Th>}
            </tr>
          }
        >
          {filtered?.map((s) => (
            <Tr key={s.id}>
              <Td><span className="font-mono font-semibold text-[var(--color-ink)]">{s.code}</span></Td>
              <Td>{s.name}</Td>
              <Td className="tabular">{s.startTime.slice(0, 5)}–{s.endTime.slice(0, 5)}</Td>
              <Td>{s.breakMinutes}m</Td>
              <Td>{s.graceInMinutes}/{s.graceOutMinutes}m</Td>
              <Td>
                {s.isOvernight
                  ? <StatusPill tone="info" label="Overnight" />
                  : <StatusPill tone="neutral" label="Day" />}
              </Td>
              <Td>
                <StatusPill tone={s.isActive ? 'success' : 'neutral'} label={s.isActive ? 'Active' : 'Inactive'} />
              </Td>
              {canWrite && (
                <Td>
                  <RowActions
                    actions={[
                      { label: 'Edit', onClick: () => setEditing(s) },
                      s.isActive
                        ? { label: 'Deactivate', tone: 'danger', onClick: () => setConfirming(s) }
                        : { label: 'Activate', onClick: () => setConfirming(s) },
                    ]}
                  />
                </Td>
              )}
            </Tr>
          ))}
        </DataTable>
      </AsyncView>

      {editing && (
        <EditShiftModal
          shift={editing}
          onClose={() => setEditing(null)}
          onSaved={() => toast('Shift updated.', 'success')}
        />
      )}

      {confirming && (
        <ConfirmDialog
          title={confirming.isActive ? `Deactivate ${confirming.code}` : `Activate ${confirming.code}`}
          message={
            confirming.isActive
              ? `Deactivate shift ${confirming.code}? It will no longer be available for new assignments.`
              : `Activate shift ${confirming.code}?`
          }
          confirmLabel={confirming.isActive ? 'Deactivate' : 'Activate'}
          tone={confirming.isActive ? 'danger' : 'primary'}
          loading={setActive.isPending}
          onConfirm={onConfirmActive}
          onCancel={() => setConfirming(null)}
        />
      )}
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
    <Card>
      <form onSubmit={onSubmit}>
        <h2 className="text-base font-semibold text-[var(--color-ink)]">New shift</h2>
        <p className="mt-1 mb-4 text-xs text-[var(--color-muted)]">Set end earlier than start for an overnight shift.</p>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Field id="shift-code" label="Code" required>
            <Input id="shift-code" aria-label="Shift code" placeholder="Code" {...register('code', { required: true })} />
          </Field>
          <Field id="shift-name" label="Name" required>
            <Input id="shift-name" aria-label="Shift name" placeholder="Name" {...register('name', { required: true })} />
          </Field>
          <Field id="shift-start" label="Start" required>
            <Input id="shift-start" type="time" {...register('startTime', { required: true })} />
          </Field>
          <Field id="shift-end" label="End" required>
            <Input id="shift-end" type="time" {...register('endTime', { required: true })} />
          </Field>
          <Field id="shift-break" label="Break (m)">
            <Input id="shift-break" type="number" {...register('breakMinutes')} />
          </Field>
          <Field id="shift-ot" label="OT threshold (m)">
            <Input id="shift-ot" type="number" {...register('overtimeThresholdMinutes')} />
          </Field>
          <Field id="shift-grace-in" label="Grace in (m)">
            <Input id="shift-grace-in" type="number" {...register('graceInMinutes')} />
          </Field>
          <Field id="shift-grace-out" label="Grace out (m)">
            <Input id="shift-grace-out" type="number" {...register('graceOutMinutes')} />
          </Field>
        </div>
        <div className="mt-5 flex items-center gap-3">
          <Button type="submit" variant="primary" loading={create.isPending} disabled={create.isPending}>Create shift</Button>
          {err && <span role="alert" className="text-sm font-medium text-[var(--color-absent)]">{err}</span>}
        </div>
      </form>
    </Card>
  );
}

interface EditShiftForm {
  name: string;
  startTime: string;
  endTime: string;
  breakMinutes: number;
  graceInMinutes: number;
  graceOutMinutes: number;
  overtimeThresholdMinutes: number;
}

function EditShiftModal({ shift, onClose, onSaved }: { shift: Shift; onClose: () => void; onSaved: () => void }) {
  const update = useUpdateShift();
  const { register, handleSubmit } = useForm<EditShiftForm>({
    defaultValues: {
      name: shift.name,
      startTime: shift.startTime.slice(0, 5),
      endTime: shift.endTime.slice(0, 5),
      breakMinutes: shift.breakMinutes,
      graceInMinutes: shift.graceInMinutes,
      graceOutMinutes: shift.graceOutMinutes,
      overtimeThresholdMinutes: shift.overtimeThresholdMinutes,
    },
  });
  const [err, setErr] = useState<string | null>(null);

  const onSubmit = handleSubmit(async (v) => {
    setErr(null);
    try {
      await update.mutateAsync({
        id: shift.id,
        code: shift.code,
        name: v.name,
        startTime: `${v.startTime}:00`,
        endTime: `${v.endTime}:00`,
        breakMinutes: Number(v.breakMinutes),
        graceInMinutes: Number(v.graceInMinutes),
        graceOutMinutes: Number(v.graceOutMinutes),
        overtimeThresholdMinutes: Number(v.overtimeThresholdMinutes),
      });
      onSaved();
      onClose();
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : 'Failed to update shift.');
    }
  });

  return (
    <Modal
      title={`Edit ${shift.code}`}
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose}>Cancel</Button>
          <Button variant="primary" loading={update.isPending} disabled={update.isPending} onClick={() => { void onSubmit(); }}>
            Save changes
          </Button>
        </>
      }
    >
      <form onSubmit={onSubmit}>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Field id="edit-name" label="Name" required className="sm:col-span-2">
            <Input id="edit-name" aria-label="Shift name" {...register('name', { required: true })} />
          </Field>
          <Field id="edit-start" label="Start">
            <Input id="edit-start" type="time" {...register('startTime', { required: true })} />
          </Field>
          <Field id="edit-end" label="End">
            <Input id="edit-end" type="time" {...register('endTime', { required: true })} />
          </Field>
          <Field id="edit-break" label="Break (m)">
            <Input id="edit-break" type="number" {...register('breakMinutes')} />
          </Field>
          <Field id="edit-ot" label="OT threshold (m)">
            <Input id="edit-ot" type="number" {...register('overtimeThresholdMinutes')} />
          </Field>
          <Field id="edit-grace-in" label="Grace in (m)">
            <Input id="edit-grace-in" type="number" {...register('graceInMinutes')} />
          </Field>
          <Field id="edit-grace-out" label="Grace out (m)">
            <Input id="edit-grace-out" type="number" {...register('graceOutMinutes')} />
          </Field>
        </div>
        {/* Hidden native submit so Enter within the form still saves. */}
        <button type="submit" className="hidden" aria-hidden="true" tabIndex={-1} />
        {err && <span role="alert" className="mt-4 block text-sm font-medium text-[var(--color-absent)]">{err}</span>}
      </form>
    </Modal>
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
    <Card>
      <form onSubmit={onSubmit}>
        <h2 className="mb-4 text-base font-semibold text-[var(--color-ink)]">Assign shift (effective-dated)</h2>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Field id="assign-shift" label="Shift">
            <Select id="assign-shift" aria-label="Shift" {...register('shiftId', { required: true })}>
              <option value="">Shift…</option>
              {shifts.data?.map((s) => <option key={s.id} value={s.id}>{s.code} — {s.name}</option>)}
            </Select>
          </Field>
          <Field id="assign-target" label="Assign to">
            <Select id="assign-target" aria-label="Assign to" {...register('target')}>
              <option value="employee">Employee</option>
              <option value="department">Department</option>
            </Select>
          </Field>
          <Field id="assign-target-id" label={target === 'employee' ? 'Employee ID' : 'Department ID'}>
            <Input
              id="assign-target-id"
              aria-label={target === 'employee' ? 'Employee ID' : 'Department ID'}
              placeholder={target === 'employee' ? 'Employee ID' : 'Department ID'}
              type="number"
              {...register('targetId', { required: true })}
            />
          </Field>
          <Field id="assign-effective" label="Effective from">
            <Input id="assign-effective" type="date" {...register('effectiveFrom', { required: true })} />
          </Field>
        </div>
        <div className="mt-5 flex items-center gap-3">
          <Button type="submit" variant="primary" loading={assign.isPending} disabled={assign.isPending}>Assign</Button>
          {err && <span role="alert" className="text-sm font-medium text-[var(--color-absent)]">{err}</span>}
        </div>
      </form>
    </Card>
  );
}
