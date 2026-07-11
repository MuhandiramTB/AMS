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
import {
  AsyncView,
  ActionIcons,
  Button,
  Card,
  ConfirmDialog,
  DataTable,
  Field,
  IconButton,
  Input,
  Modal,
  PageHeader,
  SearchInput,
  SearchableSelect,
  StatusPill,
  Td,
  Th,
  Toolbar,
  Tr,
} from '../components/ui';
import type { Device, ReconcileResult, SyncDeviceResult, TestConnectionResult } from '../api/types';

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

// The outcome of a device action, shown in a result popup.
type ActionResult = {
  title: string;
  tone: 'success' | 'warning' | 'danger';
  headline: string;
  rows: [string, string][];
};

/** Popup showing the detailed result of a Sync / Test / Reconcile action. */
function ResultModal({ result, onClose }: { result: ActionResult; onClose: () => void }) {
  const toneStyles: Record<ActionResult['tone'], { bg: string; fg: string; icon: string }> = {
    success: { bg: 'var(--color-present-bg)', fg: 'var(--color-present)', icon: 'M20 6L9 17l-5-5' },
    warning: { bg: 'var(--color-late-bg)', fg: 'var(--color-late)', icon: 'M12 9v4M12 17h.01M10.3 3.9L2.4 18a2 2 0 001.7 3h15.8a2 2 0 001.7-3L13.7 3.9a2 2 0 00-3.4 0z' },
    danger: { bg: 'var(--color-absent-bg)', fg: 'var(--color-absent)', icon: 'M18 6L6 18M6 6l12 12' },
  };
  const s = toneStyles[result.tone];
  return (
    <Modal title={result.title} onClose={onClose} size="sm" footer={<Button variant="primary" onClick={onClose}>Close</Button>}>
      <div className="mb-4 flex items-center gap-3">
        <span className="grid h-10 w-10 shrink-0 place-items-center rounded-full" style={{ background: s.bg, color: s.fg }} aria-hidden="true">
          <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="2"><path d={s.icon} strokeLinecap="round" strokeLinejoin="round" /></svg>
        </span>
        <p className="text-sm font-semibold text-[var(--color-ink)]">{result.headline}</p>
      </div>
      <dl className="divide-y divide-[var(--color-line-soft)] rounded-[var(--radius-md)] border border-[var(--color-line-soft)]">
        {result.rows.map(([k, v]) => (
          <div key={k} className="flex items-center justify-between gap-4 px-3 py-2 text-sm">
            <dt className="text-[var(--color-muted)]">{k}</dt>
            <dd className="tabular font-medium text-[var(--color-ink-soft)]">{v}</dd>
          </div>
        ))}
      </dl>
    </Modal>
  );
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
  const [result, setResult] = useState<ActionResult | null>(null);

  // Run an action and show its outcome in a popup (title, tone, detail rows).
  const runAction = async (title: string, fn: () => Promise<unknown>, describe: (r: unknown) => ActionResult) => {
    try {
      const r = await fn();
      setResult(describe(r));
    } catch (e) {
      setResult({ title, tone: 'danger', headline: 'Action failed', rows: [['Error', e instanceof ApiError ? e.message : 'Unknown error']] });
    }
  };

  const doToggle = async () => {
    try {
      if (device.isEnabled) { await disable.mutateAsync(device.id); onMessage('Device disabled.'); }
      else { await enable.mutateAsync(device.id); onMessage('Device enabled.'); }
    } catch (e) {
      onMessage(e instanceof ApiError ? e.message : 'Action failed.');
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
              <IconButton
                label="Sync"
                disabled={sync.isPending}
                icon={ActionIcons.sync}
                onClick={() =>
                  runAction('Sync', () => sync.mutateAsync(device.id), (r) => {
                    const s = r as SyncDeviceResult;
                    return s.reachable
                      ? {
                          title: 'Sync complete', tone: 'success',
                          headline: `${s.ingested} ingested`,
                          rows: [
                            ['Downloaded', String(s.downloaded)],
                            ['Ingested', String(s.ingested)],
                            ['Duplicates (skipped)', String(s.duplicates)],
                            ['Unresolved', String(s.unresolved)],
                            ['Watermark advanced', s.watermarkAdvanced ? 'Yes' : 'No'],
                          ],
                        }
                      : { title: 'Sync', tone: 'danger', headline: 'Device unreachable', rows: [['Ingested', '0'], ['Watermark', 'Preserved — nothing lost']] };
                  })
                }
              />
              <IconButton
                label="Test connection"
                disabled={test.isPending}
                icon={ActionIcons.test}
                onClick={() =>
                  runAction('Test connection', () => test.mutateAsync(device.id), (r) => {
                    const s = r as TestConnectionResult;
                    return {
                      title: 'Connection test', tone: s.reachable ? 'success' : 'danger',
                      headline: s.reachable ? 'Device reachable' : 'Device unreachable',
                      rows: [['Result', s.reachable ? 'Online' : 'Offline'], ['Message', s.message ?? '—']],
                    };
                  })
                }
              />
              <IconButton
                label="Reconcile"
                disabled={reconcile.isPending}
                icon={ActionIcons.reconcile}
                onClick={() =>
                  runAction('Reconcile', () => reconcile.mutateAsync(device.id), (r) => {
                    const s = r as ReconcileResult;
                    return {
                      title: 'Reconciliation', tone: s.clean ? 'success' : 'warning',
                      headline: s.clean ? 'Clean — device and records match' : `Gap: ${s.missingCount} missing`,
                      rows: [
                        ['On device', String(s.deviceCount)],
                        ['Stored', String(s.storedCount)],
                        ['Missing', String(s.missingCount)],
                        ['Extra', String(s.extraCount)],
                      ],
                    };
                  })
                }
              />
              {device.isEnabled ? (
                <IconButton label="Disable device" tone="danger" icon={ActionIcons.power} onClick={() => setConfirmToggle(true)} />
              ) : (
                <IconButton label="Enable device" tone="success" icon={ActionIcons.power} onClick={() => setConfirmToggle(true)} />
              )}
            </>
          )}
        </div>
      </Td>

      {result && (
        <td className="hidden">
          <ResultModal result={result} onClose={() => setResult(null)} />
        </td>
      )}

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
  const form = useForm<{ deviceUserId: string }>();
  const [err, setErr] = useState<string | null>(null);

  // ONE searchable employee dropdown (type to filter, click to pick). We load the
  // roster once and the Combobox filters it in-place — no separate search box.
  const [employeeId, setEmployeeId] = useState('');
  // The API caps pageSize at 100; load the first page as the dropdown source.
  const employees = useEmployees(1, 100);
  const employeeOptions = (employees.data?.items ?? []).map((e) => ({
    value: String(e.id),
    label: `${e.employeeNo} — ${e.firstName} ${e.lastName}`,
  }));

  const onSubmit = form.handleSubmit(async (values) => {
    setErr(null);
    if (!employeeId) { setErr('Choose an employee.'); return; }
    try {
      await enroll.mutateAsync({
        deviceId: device.id,
        employeeId: Number(employeeId),
        deviceUserId: values.deviceUserId,
      });
      form.reset();
      setEmployeeId('');
      onMessage('Employee enrolled.');
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : 'Enrollment failed.');
    }
  });

  return (
    <Card className="mt-6">
      <div className="mb-4 flex items-center gap-2">
        <span className="grid h-8 w-8 place-items-center rounded-[var(--radius-md)] bg-[var(--hdr-devices-bg)] text-[var(--hdr-devices-fg)]" aria-hidden="true">
          <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="1.8"><circle cx="9" cy="8" r="3.2" /><path d="M3.5 20a5.5 5.5 0 0111 0M17 8h4M19 6v4" strokeLinecap="round" /></svg>
        </span>
        <div>
          <h2 className="text-base font-semibold leading-tight text-[var(--color-ink)]">Enrollments — {device.name}</h2>
          <p className="text-xs text-[var(--color-muted)]">Match an employee to the user ID their fingerprint uses on this device.</p>
        </div>
      </div>

      {canManage && (
        <form onSubmit={onSubmit} className="mb-6 rounded-[var(--radius-lg)] border border-[var(--color-line)] bg-[var(--color-surface-2)] p-4">
          <div className="flex flex-wrap items-end gap-3">
            <Field id="enr-emp" label="Employee" className="w-72">
              <SearchableSelect
                id="enr-emp"
                options={employeeOptions}
                value={employeeId}
                onChange={setEmployeeId}
                placeholder="Select employee…"
                searchPlaceholder="Search by name or no…"
                emptyText="No matching employee"
              />
            </Field>
            <Field id="enr-user" label="Device user ID" className="w-32" hint="From the device">
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
          </div>
          {err && (
            <p role="alert" className="mt-3 text-sm font-medium text-[var(--color-danger)]">
              {err}
            </p>
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
