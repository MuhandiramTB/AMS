import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AttendancePage } from './AttendancePage';
import { ApiError } from '../api/client';
import * as AuthModule from '../auth/AuthContext';
import * as Hooks from '../api/hooks';
import type { AttendanceRecord } from '../api/types';

const record: AttendanceRecord = {
  id: 1, employeeId: 1, workDate: '2026-07-10', resolvedShiftId: 1,
  firstInUtc: '2026-07-10T09:25:00Z', lastOutUtc: '2026-07-10T17:00:00Z',
  workedMinutes: 395, lateMinutes: 15, earlyLeaveMinutes: 0, overtimeMinutes: 0,
  status: 'Exception', exceptions: [{ id: 1, type: 'MissingOut', isResolved: false, notes: null }],
  concurrencyToken: 'AAAAAAAAB9o=',
};

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}><AttendancePage /></QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.restoreAllMocks();
  vi.spyOn(AuthModule, 'useAuth').mockReturnValue({
    user: { id: 9, userName: 'hr', roles: ['HROfficer'], permissions: ['Attendance.Read', 'Attendance.Correct'] },
    isAuthenticated: true, isInitialising: false,
    hasPermission: (p) => ['Attendance.Read', 'Attendance.Correct'].includes(p),
    login: vi.fn(), logout: vi.fn(),
  });
  // Records list returns our single exception record.
  vi.spyOn(Hooks, 'useAttendanceRecords').mockReturnValue({
    data: { items: [record], page: 1, pageSize: 15, totalCount: 1, totalPages: 1 },
    isLoading: false, isError: false, isPlaceholderData: false,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
  } as any);
});

describe('Attendance correction drawer', () => {
  it('blocks submit without a reason (BRULE-05) and shows a validation message', async () => {
    const mutateAsync = vi.fn();
    vi.spyOn(Hooks, 'useCorrectAttendance').mockReturnValue(
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      { mutateAsync, isPending: false } as any,
    );

    const user = userEvent.setup();
    renderPage();
    await user.click(screen.getByRole('button', { name: 'Review' }));      // open drawer
    await user.click(screen.getByRole('button', { name: /save & recalculate/i }));

    // Reason is required → the mutation must NOT be called.
    expect(mutateAsync).not.toHaveBeenCalled();
    expect(await screen.findByText(/a reason is required/i)).toBeInTheDocument();
  });

  it('shows a reload message when the server returns 409 (stale ETag)', async () => {
    const mutateAsync = vi.fn().mockRejectedValue(new ApiError(409, 'conflict'));
    vi.spyOn(Hooks, 'useCorrectAttendance').mockReturnValue(
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      { mutateAsync, isPending: false } as any,
    );

    const user = userEvent.setup();
    renderPage();
    await user.click(screen.getByRole('button', { name: 'Review' }));
    await user.type(screen.getByLabelText(/reason/i), 'CCTV confirms');
    await user.click(screen.getByRole('button', { name: /save & recalculate/i }));

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledOnce());
    expect(await screen.findByText(/changed since you opened it/i)).toBeInTheDocument();
  });

  it('passes the record ETag as ifMatch on a valid correction', async () => {
    const mutateAsync = vi.fn().mockResolvedValue(record);
    vi.spyOn(Hooks, 'useCorrectAttendance').mockReturnValue(
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      { mutateAsync, isPending: false } as any,
    );

    const user = userEvent.setup();
    renderPage();
    await user.click(screen.getByRole('button', { name: 'Review' }));
    await user.type(screen.getByLabelText(/reason/i), 'forgot to punch out');
    await user.click(screen.getByRole('button', { name: /save & recalculate/i }));

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledOnce());
    expect(mutateAsync).toHaveBeenCalledWith(
      expect.objectContaining({ id: 1, reason: 'forgot to punch out', ifMatch: 'AAAAAAAAB9o=' }),
    );
  });
});
