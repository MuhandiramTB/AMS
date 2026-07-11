import { useState } from 'react';
import { useForm } from 'react-hook-form';
import {
  useDevices,
  useDeviceEnrollments,
  useDisableDevice,
  useEmployees,
  useEnableDevice,
  useEnrollEmployee,
  useReconcileDevice,
  useRegisterDevice,
  useSyncDevice,
  useTestDevice,
  type RegisterDeviceInput,
} from '../api/hooks';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../api/client';
import { useDebounced } from '../lib/useDebounced';
import {
  AsyncView,
  Button,
  Card,
  ConfirmDialog,
  DataTable,
  Field,
  Input,
  PageHeader,
  SearchInput,
  Select,
  StatusPill,
  Td,
  Th,
  Toolbar,
  Tr,
} from '../components/ui';
import type { Device } from '../api/types';

function relativeTime(iso: string | null): string {
  if (!iso) return 'never';
  const diffMs = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diffMs / 60000);
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  return `${Math.floor(hrs / 24)}d ago`;
}

// A device is considered "online" if seen within the last ~2 minutes.
function isOnline(d: Device): boolean {
  if (!d.lastSeenUtc) return false;
  return Date.now() - new Date(d.lastSeenUtc).getTime() < 2 * 60 * 1000;
}

export function DevicesPage() {
  const { hasPermission } = useAuth();
  const canManage = hasPermission('Device.Manage');
  const devices = useDevices();
  const [message, setMessage] = useState<string | null>(null);
  const [selected, setSelected] = useState<Device | null>(null);
  const [query, setQuery] = useState('');

  const q = query.trim().toLowerCase();
  const filtered = (devices.data ?? []).filter(
    (d) =>
      q === '' ||
      d.name.toLowerCase().includes(q) ||
      d.serialNo.toLowerCase().includes(q),
  );
  const hasDevices = (devices.data?.length ?? 0) > 0;

  return (
    <div>
      <PageHeader
        title="Devices"
        subtitle="ZKTeco terminals. Health refreshes automatically; an offline device recovers its missed punches on reconnect — no data is lost meanwhile."
      />

      {message && (
        <p
          className="mb-4 rounded-[var(--radius-md)] bg-[var(--color-surface-2)] px-4 py-3 text-sm text-[var(--color-ink-soft)]"
          role="status"
        >
          {message}
        </p>
      )}

      {canManage && <RegisterDeviceForm onDone={(m) => setMessage(m)} />}

      <AsyncView
        isLoading={devices.isLoading}
        isError={devices.isError}
        isEmpty={devices.data?.length === 0}
        emptyText="No devices registered yet."
      >
        {hasDevices && (
          <Toolbar className="mb-4">
            <SearchInput
              value={query}
              onChange={setQuery}
              label="Search devices"
              placeholder="Name or serial…"
            />
          </Toolbar>
        )}
        <DataTable
          head={
            <tr>
              <Th module="devices">Name</Th>
              <Th module="devices">Serial</Th>
              <Th module="devices">Status</Th>
              <Th module="devices">Last seen</Th>
              <Th module="devices">Actions</Th>
            </tr>
          }
        >
          {filtered.length === 0 ? (
            <tr>
              <Td>
                <span className="text-[var(--color-muted)]">No devices match your search.</span>
              </Td>
            </tr>
          ) : (
            filtered.map((d) => (
              <DeviceRow
                key={d.id}
                device={d}
                canManage={canManage}
                onMessage={setMessage}
                onSelect={() => setSelected(d)}
                selected={selected?.id === d.id}
              />
            ))
          )}
        </DataTable>
      </AsyncView>

      {selected && <EnrollmentPanel device={selected} canManage={canManage} onMessage={setMessage} />}
    </div>
  );
}

function DeviceRow({
  device,
  canManage,
  onMessage,
  onSelect,
  selected,
}: {
  device: Device;
  canManage: boolean;
  onMessage: (m: string) => void;
  onSelect: () => void;
  selected: boolean;
}) {
  const sync = useSyncDevice();
  const test = useTestDevice();
  const reconcile = useReconcileDevice();
  const enable = useEnableDevice();
  const disable = useDisableDevice();
  const online = isOnline(device);
  const [confirmToggle, setConfirmToggle] = useState(false);

  const run = async (fn: () => Promise<unknown>, describe: (r: unknown) => string) => {
    try {
      const r = await fn();
      onMessage(describe(r));
    } catch (e) {
      onMessage(e instanceof ApiError ? e.message : 'Action failed.');
    }
  };

  const doToggle = async () => {
    if (device.isEnabled) {
      await run(() => disable.mutateAsync(device.id), () => 'Device disabled.');
    } else {
      await run(() => enable.mutateAsync(device.id), () => 'Device enabled.');
    }
    setConfirmToggle(false);
  };

  return (
    <Tr selected={selected}>
      <Td>
        <button
          className="font-medium text-[var(--color-brand-600)] hover:underline"
          onClick={onSelect}
        >
          {device.name}
        </button>
      </Td>
      <Td className="font-mono text-xs text-[var(--color-muted)]">{device.serialNo}</Td>
      <Td>
        {!device.isEnabled ? (
          <StatusPill tone="neutral" label="Disabled" />
        ) : online ? (
          <StatusPill tone="success" label="Online" />
        ) : (
          <StatusPill tone="danger" label="Offline" />
        )}
      </Td>
      <Td className="text-[var(--color-muted)]">{relativeTime(device.lastSeenUtc)}</Td>
      <Td>
        <div className="flex flex-wrap gap-1.5">
          {canManage && (
            <>
              <Button
                size="sm"
                loading={sync.isPending}
                onClick={() =>
                  run(
                    () => sync.mutateAsync(device.id),
                    (r) => {
                      const s = r as { ingested: number; reachable: boolean };
                      return s.reachable ? `Synced: ${s.ingested} ingested.` : 'Device unreachable.';
                    },
                  )
                }
              >
                Sync
              </Button>
              <Button
                size="sm"
                loading={test.isPending}
                onClick={() =>
                  run(
                    () => test.mutateAsync(device.id),
                    (r) => {
                      const s = r as { reachable: boolean };
                      return s.reachable ? 'Reachable.' : 'Unreachable.';
                    },
                  )
                }
              >
                Test
              </Button>
              <Button
                size="sm"
                loading={reconcile.isPending}
                onClick={() =>
                  run(
                    () => reconcile.mutateAsync(device.id),
                    (r) => {
                      const s = r as { clean: boolean; missingCount: number };
                      return s.clean ? 'Reconciliation clean.' : `Gap: ${s.missingCount} missing!`;
                    },
                  )
                }
              >
                Reconcile
              </Button>
              {device.isEnabled ? (
                <Button size="sm" variant="danger" onClick={() => setConfirmToggle(true)}>Disable</Button>
              ) : (
                <Button size="sm" variant="primary" onClick={() => setConfirmToggle(true)}>Enable</Button>
              )}
            </>
          )}
        </div>
      </Td>

      {confirmToggle && (
        <td className="hidden">
          <ConfirmDialog
            title={device.isEnabled ? 'Disable device?' : 'Enable device?'}
            tone={device.isEnabled ? 'danger' : 'primary'}
            confirmLabel={device.isEnabled ? 'Disable' : 'Enable'}
            loading={enable.isPending || disable.isPending}
            message={
              device.isEnabled
                ? <>Disable <strong>{device.name}</strong>? The worker will stop polling it for punches.</>
                : <>Enable <strong>{device.name}</strong>? The worker will resume polling it.</>
            }
            onConfirm={doToggle}
            onCancel={() => setConfirmToggle(false)}
          />
        </td>
      )}
    </Tr>
  );
}

function RegisterDeviceForm({ onDone }: { onDone: (m: string) => void }) {
  const register = useRegisterDevice();
  const form = useForm<RegisterDeviceInput>();
  const [err, setErr] = useState<string | null>(null);

  const onSubmit = form.handleSubmit(async (values) => {
    setErr(null);
    try {
      await register.mutateAsync({ ...values, port: values.port ? Number(values.port) : null });
      form.reset();
      onDone(`Device '${values.name}' registered.`);
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : 'Failed to register.');
    }
  });

  return (
    <Card className="mb-6">
      <h2 className="mb-4 text-sm font-semibold text-[var(--color-ink)]">Register a device</h2>
      <form onSubmit={onSubmit} className="flex flex-wrap items-end gap-3">
        <Field id="reg-serial" label="Serial number" className="w-40">
          <Input
            id="reg-serial"
            aria-label="Serial number"
            placeholder="Serial no"
            {...form.register('serialNo', { required: true })}
          />
        </Field>
        <Field id="reg-name" label="Device name" className="w-40">
          <Input
            id="reg-name"
            aria-label="Device name"
            placeholder="Name"
            {...form.register('name', { required: true })}
          />
        </Field>
        <Field id="reg-ip" label="IP address" className="w-40">
          <Input
            id="reg-ip"
            aria-label="IP address (optional)"
            placeholder="IP (optional)"
            {...form.register('ipAddress')}
          />
        </Field>
        <Field id="reg-port" label="Port" className="w-24">
          <Input
            id="reg-port"
            aria-label="Port"
            placeholder="Port"
            type="number"
            {...form.register('port')}
          />
        </Field>
        <Button type="submit" variant="primary" loading={register.isPending}>
          Register device
        </Button>
        {err && (
          <span role="alert" className="text-sm font-medium text-[var(--color-danger)]">
            {err}
          </span>
        )}
      </form>
    </Card>
  );
}

function EnrollmentPanel({
  device,
  canManage,
  onMessage,
}: {
  device: Device;
  canManage: boolean;
  onMessage: (m: string) => void;
}) {
  const enrollments = useDeviceEnrollments(device.id);
  const enroll = useEnrollEmployee();
  const form = useForm<{ employeeId: string; deviceUserId: string }>();
  const [err, setErr] = useState<string | null>(null);

  // Employee picker: search by name/number (server-side) → choose from a dropdown,
  // so an admin never has to know the raw employee id.
  const [empSearch, setEmpSearch] = useState('');
  const debounced = useDebounced(empSearch);
  const employees = useEmployees(1, 50, undefined, debounced);
  const employeeList = employees.data?.items ?? [];

  const onSubmit = form.handleSubmit(async (values) => {
    setErr(null);
    try {
      await enroll.mutateAsync({
        deviceId: device.id,
        employeeId: Number(values.employeeId),
        deviceUserId: values.deviceUserId,
      });
      form.reset();
      setEmpSearch('');
      onMessage('Employee enrolled.');
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : 'Enrollment failed.');
    }
  });

  return (
    <Card className="mt-6">
      <h2 className="mb-1 text-base font-semibold text-[var(--color-ink)]">
        Enrollments — {device.name}
      </h2>
      <p className="mb-4 text-xs text-[var(--color-muted)]">
        Link an employee to the ID their fingerprint was registered under on this device.
      </p>

      {canManage && (
        <form onSubmit={onSubmit} className="mb-5 flex flex-wrap items-end gap-3">
          <SearchInput
            value={empSearch}
            onChange={setEmpSearch}
            label="Find employee"
            placeholder="Name or employee no…"
            className="min-w-56 flex-none"
          />
          <Field id="enr-emp" label="Employee" className="w-56">
            <Select id="enr-emp" {...form.register('employeeId', { required: true })}>
              <option value="">Select employee…</option>
              {employeeList.map((e) => (
                <option key={e.id} value={e.id}>{e.employeeNo} — {e.firstName} {e.lastName}</option>
              ))}
            </Select>
          </Field>
          <Field id="enr-user" label="Device user ID" className="w-40" hint="The number from the device">
            <Input
              id="enr-user"
              aria-label="Device user ID"
              placeholder="e.g. 5"
              {...form.register('deviceUserId', { required: true })}
            />
          </Field>
          <Button type="submit" variant="primary" loading={enroll.isPending}>
            Enroll
          </Button>
          {err && (
            <span role="alert" className="text-sm font-medium text-[var(--color-danger)]">
              {err}
            </span>
          )}
        </form>
      )}

      <AsyncView
        isLoading={enrollments.isLoading}
        isError={enrollments.isError}
        isEmpty={enrollments.data?.length === 0}
        emptyText="No enrollments on this device."
      >
        <DataTable
          head={
            <tr>
              <Th module="devices">Employee ID</Th>
              <Th module="devices">Device User ID</Th>
              <Th module="devices">Status</Th>
            </tr>
          }
        >
          {enrollments.data?.map((e) => (
            <Tr key={e.id}>
              <Td>{e.employeeId}</Td>
              <Td className="font-mono">{e.deviceUserId}</Td>
              <Td>
                <StatusPill
                  tone={e.isActive ? 'success' : 'neutral'}
                  label={e.isActive ? 'Active' : 'Inactive'}
                />
              </Td>
            </Tr>
          ))}
        </DataTable>
      </AsyncView>
    </Card>
  );
}
