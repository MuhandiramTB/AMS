import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { DevicesPage } from './DevicesPage';
import { ApiError } from '../api/client';
import * as AuthModule from '../auth/AuthContext';
import * as Hooks from '../api/hooks';
import type { Device } from '../api/types';

const nowIso = () => new Date().toISOString();
const oldIso = () => new Date(Date.now() - 10 * 60 * 1000).toISOString(); // 10 min ago

function device(over: Partial<Device>): Device {
  return {
    id: 1, serialNo: 'ZK-1', name: 'Gate A', ipAddress: '10.0.0.5', port: 4370,
    model: 'K40', isEnabled: true, lastSeenUtc: nowIso(), ...over,
  };
}

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}><DevicesPage /></QueryClientProvider>);
}

// Mocks for the per-row action hooks + enrollment hooks (all no-ops unless overridden).
function stubDeviceActionHooks() {
  const noop = () => ({ mutateAsync: vi.fn(), isPending: false });
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useTestDevice').mockReturnValue(noop() as any);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useReconcileDevice').mockReturnValue(noop() as any);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useEnableDevice').mockReturnValue(noop() as any);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useDisableDevice').mockReturnValue(noop() as any);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useRegisterDevice').mockReturnValue(noop() as any);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useEnrollEmployee').mockReturnValue(noop() as any);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useDeviceEnrollments').mockReturnValue({ data: [], isLoading: false, isError: false } as any);
}

beforeEach(() => {
  vi.restoreAllMocks();
  vi.spyOn(AuthModule, 'useAuth').mockReturnValue({
    user: { id: 1, userName: 'admin', roles: ['Administrator'], permissions: ['Device.Read', 'Device.Manage'] },
    isAuthenticated: true, isInitialising: false,
    hasPermission: (p) => ['Device.Read', 'Device.Manage'].includes(p),
    login: vi.fn(), logout: vi.fn(),
  });
  stubDeviceActionHooks();
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useSyncDevice').mockReturnValue({ mutateAsync: vi.fn(), isPending: false } as any);
});

describe('DevicesPage health status logic', () => {
  it('shows Online for a recently-seen enabled device', () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useDevices').mockReturnValue({ data: [device({ lastSeenUtc: nowIso() })], isLoading: false, isError: false } as any);
    renderPage();
    expect(screen.getByText('Online')).toBeInTheDocument();
  });

  it('shows Offline for an enabled device not seen recently', () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useDevices').mockReturnValue({ data: [device({ lastSeenUtc: oldIso() })], isLoading: false, isError: false } as any);
    renderPage();
    expect(screen.getByText('Offline')).toBeInTheDocument();
  });

  it('shows Disabled regardless of last-seen when the device is disabled', () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useDevices').mockReturnValue({ data: [device({ isEnabled: false, lastSeenUtc: nowIso() })], isLoading: false, isError: false } as any);
    renderPage();
    expect(screen.getByText('Disabled')).toBeInTheDocument();
  });

  it('shows the empty state when there are no devices', () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useDevices').mockReturnValue({ data: [], isLoading: false, isError: false } as any);
    renderPage();
    expect(screen.getByText(/no devices registered/i)).toBeInTheDocument();
  });
});

describe('DevicesPage actions', () => {
  it('registering a duplicate serial surfaces the 409 message', async () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useDevices').mockReturnValue({ data: [], isLoading: false, isError: false } as any);
    const mutateAsync = vi.fn().mockRejectedValue(new ApiError(409, "A device with serial 'ZK-1' is already registered."));
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useRegisterDevice').mockReturnValue({ mutateAsync, isPending: false } as any);

    const user = userEvent.setup();
    renderPage();
    await user.type(screen.getByPlaceholderText('Serial no'), 'ZK-1');
    await user.type(screen.getByPlaceholderText('Name'), 'Gate');
    await user.click(screen.getByRole('button', { name: /register device/i }));

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledOnce());
    expect(await screen.findByText(/already registered/i)).toBeInTheDocument();
  });

  it('clicking Sync reports the ingested count', async () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useDevices').mockReturnValue({ data: [device({})], isLoading: false, isError: false } as any);
    const mutateAsync = vi.fn().mockResolvedValue({ deviceId: 1, reachable: true, ingested: 3, duplicates: 0, unresolved: 0, watermarkAdvanced: true, alerted: false, downloaded: 3 });
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useSyncDevice').mockReturnValue({ mutateAsync, isPending: false } as any);

    const user = userEvent.setup();
    renderPage();
    await user.click(screen.getByRole('button', { name: 'Sync' }));

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledWith(1));
    expect(await screen.findByText(/3 ingested/i)).toBeInTheDocument();
  });
});
