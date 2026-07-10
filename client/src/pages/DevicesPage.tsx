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
import { AsyncView, Button, StatusPill } from '../components/ui';
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
      <h1 className="mb-1 text-2xl font-semibold text-slate-800">Devices</h1>
      <p className="mb-4 text-sm text-slate-500">
        ZKTeco terminals. Health refreshes automatically; an offline device recovers its
        missed punches on reconnect — no data is lost meanwhile.
      </p>

      {message && (
        <p className="mb-4 rounded bg-slate-100 px-3 py-2 text-sm text-slate-700" role="status">
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
        <table className="w-full border-collapse text-left text-sm">
          <thead>
            <tr className="border-b border-slate-200 text-slate-500">
              <th className="py-2">Name</th>
              <th className="py-2">Serial</th>
              <th className="py-2">Status</th>
              <th className="py-2">Last seen</th>
              <th className="py-2">Actions</th>
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
        </table>
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
    <tr className={`border-b border-slate-100 ${selected ? 'bg-blue-50' : ''}`}>
      <td className="py-2">
        <button className="text-blue-700 hover:underline" onClick={onSelect}>{device.name}</button>
      </td>
      <td className="py-2 font-mono text-xs">{device.serialNo}</td>
      <td className="py-2">
        {!device.isEnabled ? (
          <StatusPill tone="neutral" label="Disabled" />
        ) : online ? (
          <StatusPill tone="success" label="Online" />
        ) : (
          <StatusPill tone="danger" label="Offline" />
        )}
      </td>
      <td className="py-2 text-slate-500">{relativeTime(device.lastSeenUtc)}</td>
      <td className="py-2">
        <div className="flex flex-wrap gap-1">
          {canManage && (
            <>
              <Button onClick={() => run(
                () => sync.mutateAsync(device.id),
                (r) => { const s = r as { ingested: number; reachable: boolean };
                  return s.reachable ? `Synced: ${s.ingested} ingested.` : 'Device unreachable.'; },
              )}>Sync</Button>
              <Button onClick={() => run(
                () => test.mutateAsync(device.id),
                (r) => { const s = r as { reachable: boolean }; return s.reachable ? 'Reachable.' : 'Unreachable.'; },
              )}>Test</Button>
              <Button onClick={() => run(
                () => reconcile.mutateAsync(device.id),
                (r) => { const s = r as { clean: boolean; missingCount: number };
                  return s.clean ? 'Reconciliation clean.' : `Gap: ${s.missingCount} missing!`; },
              )}>Reconcile</Button>
              {device.isEnabled ? (
                <Button variant="danger" onClick={() => run(() => disable.mutateAsync(device.id), () => 'Device disabled.')}>Disable</Button>
              ) : (
                <Button onClick={() => run(() => enable.mutateAsync(device.id), () => 'Device enabled.')}>Enable</Button>
              )}
            </>
          )}
        </div>
      </td>
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
    <form onSubmit={onSubmit} className="mb-6 flex flex-wrap items-end gap-2">
      <input placeholder="Serial no" className="rounded border border-slate-300 px-2 py-1" {...form.register('serialNo', { required: true })} />
      <input placeholder="Name" className="rounded border border-slate-300 px-2 py-1" {...form.register('name', { required: true })} />
      <input placeholder="IP (optional)" className="rounded border border-slate-300 px-2 py-1" {...form.register('ipAddress')} />
      <input placeholder="Port" type="number" className="w-24 rounded border border-slate-300 px-2 py-1" {...form.register('port')} />
      <Button type="submit" variant="primary" disabled={register.isPending}>Register device</Button>
      {err && <span role="alert" className="text-sm text-red-600">{err}</span>}
    </form>
  );
}

function EnrollmentPanel({ device, canManage, onMessage }: { device: Device; canManage: boolean; onMessage: (m: string) => void }) {
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
    <div className="mt-6 rounded border border-slate-200 p-4">
      <h2 className="mb-3 text-lg font-medium text-slate-800">Enrollments — {device.name}</h2>

      {canManage && (
        <form onSubmit={onSubmit} className="mb-4 flex flex-wrap items-end gap-2">
          <input placeholder="Employee ID" type="number" className="rounded border border-slate-300 px-2 py-1" {...form.register('employeeId', { required: true })} />
          <input placeholder="Device user ID" className="rounded border border-slate-300 px-2 py-1" {...form.register('deviceUserId', { required: true })} />
          <Button type="submit" variant="primary" disabled={enroll.isPending}>Enroll</Button>
          {err && <span role="alert" className="text-sm text-red-600">{err}</span>}
        </form>
      )}

      <AsyncView
        isLoading={enrollments.isLoading}
        isError={enrollments.isError}
        isEmpty={enrollments.data?.length === 0}
        emptyText="No enrollments on this device."
      >
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b border-slate-200 text-slate-500">
              <th className="py-1">Employee ID</th><th className="py-1">Device User ID</th><th className="py-1">Status</th>
            </tr>
          </thead>
          <tbody>
            {enrollments.data?.map((e) => (
              <tr key={e.id} className="border-b border-slate-100">
                <td className="py-1">{e.employeeId}</td>
                <td className="py-1 font-mono">{e.deviceUserId}</td>
                <td className="py-1">
                  <StatusPill tone={e.isActive ? 'success' : 'neutral'} label={e.isActive ? 'Active' : 'Inactive'} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </AsyncView>
    </div>
  );
}
