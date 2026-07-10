import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClientProvider, QueryClient } from '@tanstack/react-query';
import { ShiftsPage } from './ShiftsPage';
import { ApiError } from '../api/client';
import * as AuthModule from '../auth/AuthContext';
import * as Hooks from '../api/hooks';
import type { Shift } from '../api/types';

const dayShift: Shift = {
  id: 1, code: 'DAY', name: 'Day', startTime: '09:00:00', endTime: '17:00:00',
  breakMinutes: 60, graceInMinutes: 10, graceOutMinutes: 10,
  overtimeThresholdMinutes: 0, isOvernight: false, isActive: true,
};
const nightShift: Shift = { ...dayShift, id: 2, code: 'NIGHT', name: 'Night', startTime: '22:00:00', endTime: '06:00:00', isOvernight: true };

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}><ShiftsPage /></QueryClientProvider>);
}

beforeEach(() => {
  vi.restoreAllMocks();
  vi.spyOn(AuthModule, 'useAuth').mockReturnValue({
    user: { id: 1, userName: 'hr', roles: ['HROfficer'], permissions: ['Shift.Read', 'Shift.Write'] },
    isAuthenticated: true, isInitialising: false,
    hasPermission: (p) => ['Shift.Read', 'Shift.Write'].includes(p),
    login: vi.fn(), logout: vi.fn(),
  });
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useShifts').mockReturnValue({ data: [dayShift, nightShift], isLoading: false, isError: false } as any);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useAssignShift').mockReturnValue({ mutateAsync: vi.fn(), isPending: false } as any);
});

describe('ShiftsPage', () => {
  it('renders shifts and flags overnight vs day (status by label, not colour)', () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useCreateShift').mockReturnValue({ mutateAsync: vi.fn(), isPending: false } as any);
    renderPage();
    expect(screen.getByText('DAY')).toBeInTheDocument();   // code (unique)
    expect(screen.getByText('NIGHT')).toBeInTheDocument();
    // Overnight pill (unique). "Day" appears both as the shift name and the day
    // status pill, so assert at least one is present.
    expect(screen.getByText('Overnight')).toBeInTheDocument();
    expect(screen.getAllByText('Day').length).toBeGreaterThan(0);
  });

  it('creates a shift, converting HH:mm inputs to HH:mm:ss and numeric fields to numbers', async () => {
    const mutateAsync = vi.fn().mockResolvedValue(dayShift);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useCreateShift').mockReturnValue({ mutateAsync, isPending: false } as any);

    const user = userEvent.setup();
    renderPage();
    await user.type(screen.getByPlaceholderText('Code'), 'EVE');
    await user.type(screen.getByPlaceholderText('Name'), 'Evening');
    // time inputs: find by their type via display; use container query
    const times = document.querySelectorAll('input[type="time"]');
    await user.type(times[0] as HTMLInputElement, '13:00');
    await user.type(times[1] as HTMLInputElement, '21:00');
    await user.click(screen.getByRole('button', { name: /create shift/i }));

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledOnce());
    const arg = mutateAsync.mock.calls[0][0];
    expect(arg.code).toBe('EVE');
    expect(arg.startTime).toBe('13:00:00');   // HH:mm → HH:mm:ss
    expect(arg.endTime).toBe('21:00:00');
    expect(typeof arg.breakMinutes).toBe('number'); // string→number
  });

  it('shows the server conflict message when an overlapping assignment is rejected (409)', async () => {
    const mutateAsync = vi.fn().mockRejectedValue(new ApiError(409, 'An overlapping shift assignment already exists.'));
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useAssignShift').mockReturnValue({ mutateAsync, isPending: false } as any);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useCreateShift').mockReturnValue({ mutateAsync: vi.fn(), isPending: false } as any);

    const user = userEvent.setup();
    renderPage();
    // Fill the assign form: the shift <select> is the one that has the "DAY — Day"
    // option; select by that option's visible label.
    const shiftSelect = screen.getByRole('option', { name: /DAY — Day/ }).closest('select') as HTMLSelectElement;
    await user.selectOptions(shiftSelect, '1');
    await user.type(screen.getByPlaceholderText('Employee ID'), '5');
    const dateInput = document.querySelector('input[type="date"]') as HTMLInputElement;
    await user.type(dateInput, '2026-08-01');
    await user.click(screen.getByRole('button', { name: /^assign$/i }));

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledOnce());
    expect(await screen.findByText(/overlapping shift assignment/i)).toBeInTheDocument();
  });
});
