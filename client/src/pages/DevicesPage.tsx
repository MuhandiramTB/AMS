import { useState } from 'react';
import { useForm } from 'react-hook-form';
import {
  useDevices,
  useDeviceEnrollments,
  useDisableDevice,
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
import {
  AsyncView,
  Button,
  Card,
  Field,
  Input,
  PageHeader,
  StatusPill,
  TableWrap,
  Td,
  Th,
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
        <TableWrap>
          <thead>
            <tr>
              <Th>Name</Th>
              <Th>Serial</Th>
              <Th>Status</Th>
              <Th>Last seen</Th>
              <Th>Actions</Th>
            </tr>
          </thead>
          <tbody>
            {devices.data?.map((d) => (
              <DeviceRow
                key={d.id}
                device={d}
                canManage={canManage}
                onMessage={setMessage}
                onSelect={() => setSelected(d)}
                selected={selected?.id === d.id}
              />
            ))}
          </tbody>
        </TableWrap>
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

  const run = async (fn: () => Promise<unknown>, describe: (r: unknown) => string) => {
    try {
      const r = await fn();
      onMessage(describe(r));
    } catch (e) {
      onMessage(e instanceof ApiError ? e.message : 'Action failed.');
    }
  };

  return (
    <tr
      className={`border-t border-[var(--color-line-soft)] transition-colors hover:bg-[var(--color-surface-2)] ${
        selected ? 'bg-[var(--color-brand-600)]/5' : ''
      }`}
    >
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
                <Button
                  size="sm"
                  variant="danger"
                  loading={disable.isPending}
                  onClick={() => run(() => disable.mutateAsync(device.id), () => 'Device disabled.')}
                >
                  Disable
                </Button>
              ) : (
                <Button
                  size="sm"
                  variant="primary"
                  loading={enable.isPending}
                  onClick={() => run(() => enable.mutateAsync(device.id), () => 'Device enabled.')}
                >
                  Enable
                </Button>
              )}
            </>
          )}
        </div>
      </Td>
    </tr>
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

  const onSubmit = form.handleSubmit(async (values) => {
    setErr(null);
    try {
      await enroll.mutateAsync({
        deviceId: device.id,
        employeeId: Number(values.employeeId),
        deviceUserId: values.deviceUserId,
      });
      form.reset();
      onMessage('Employee enrolled.');
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : 'Enrollment failed.');
    }
  });

  return (
    <Card className="mt-6">
      <h2 className="mb-4 text-base font-semibold text-[var(--color-ink)]">
        Enrollments — {device.name}
      </h2>

      {canManage && (
        <form onSubmit={onSubmit} className="mb-5 flex flex-wrap items-end gap-3">
          <Field id="enr-emp" label="Employee ID" className="w-40">
            <Input
              id="enr-emp"
              aria-label="Employee ID"
              placeholder="Employee ID"
              type="number"
              {...form.register('employeeId', { required: true })}
            />
          </Field>
          <Field id="enr-user" label="Device user ID" className="w-40">
            <Input
              id="enr-user"
              aria-label="Device user ID"
              placeholder="Device user ID"
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
        <TableWrap>
          <thead>
            <tr>
              <Th>Employee ID</Th>
              <Th>Device User ID</Th>
              <Th>Status</Th>
            </tr>
          </thead>
          <tbody>
            {enrollments.data?.map((e) => (
              <tr
                key={e.id}
                className="border-t border-[var(--color-line-soft)] transition-colors hover:bg-[var(--color-surface-2)]"
              >
                <Td>{e.employeeId}</Td>
                <Td className="font-mono">{e.deviceUserId}</Td>
                <Td>
                  <StatusPill
                    tone={e.isActive ? 'success' : 'neutral'}
                    label={e.isActive ? 'Active' : 'Inactive'}
                  />
                </Td>
              </tr>
            ))}
          </tbody>
        </TableWrap>
      </AsyncView>
    </Card>
  );
}
