import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { LeavePage } from './LeavePage';
import { ApiError } from '../api/client';
import * as AuthModule from '../auth/AuthContext';
import * as Hooks from '../api/hooks';
import type { LeaveRequest, PagedResult } from '../api/types';

function req(over: Partial<LeaveRequest>): LeaveRequest {
  return {
    id: 1, employeeId: 5, leaveTypeId: 2, startDate: '2026-07-20', endDate: '2026-07-22',
    dayCount: 3, status: 'Submitted', approverUserId: null, reason: 'trip', ...over,
  };
}
function paged(items: LeaveRequest[]): PagedResult<LeaveRequest> {
  return { items, page: 1, pageSize: 15, totalCount: items.length, totalPages: 1 };
}

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}><LeavePage /></QueryClientProvider>);
}

function mockAuth(permissions: string[]) {
  vi.spyOn(AuthModule, 'useAuth').mockReturnValue({
    user: { id: 1, userName: 'u', roles: [], permissions },
    isAuthenticated: true, isInitialising: false,
    hasPermission: (p) => permissions.includes(p),
    login: vi.fn(), logout: vi.fn(),
  });
}

function stubActionHooks() {
  const noop = () => ({ mutateAsync: vi.fn(), isPending: false });
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useApproveLeave').mockReturnValue(noop() as any);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useRejectLeave').mockReturnValue(noop() as any);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useCancelLeave').mockReturnValue(noop() as any);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useRequestLeave').mockReturnValue(noop() as any);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useLeaveTypes').mockReturnValue({ data: [{ id: 2, code: 'AN', name: 'Annual', isActive: true }], isLoading: false, isError: false } as any);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useLeaveBalances').mockReturnValue({ data: [], isLoading: false, isError: false } as any);
}

beforeEach(() => {
  vi.restoreAllMocks();
  stubActionHooks();
});

describe('LeavePage permission gating', () => {
  it('an employee (Request only) sees the request form and Cancel, but not Approve/Reject', () => {
    mockAuth(['Leave.Read', 'Leave.Request']);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useLeaveRequests').mockReturnValue({ data: paged([req({})]), isLoading: false, isError: false, isPlaceholderData: false } as any);
    renderPage();
    expect(screen.getByRole('button', { name: /request leave/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^approve$/i })).not.toBeInTheDocument();
  });

  it('an approver (Approve only) sees Approve/Reject but not the request form', () => {
    mockAuth(['Leave.Read', 'Leave.Approve']);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useLeaveRequests').mockReturnValue({ data: paged([req({})]), isLoading: false, isError: false, isPlaceholderData: false } as any);
    renderPage();
    expect(screen.queryByRole('button', { name: /request leave/i })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^approve$/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^reject$/i })).toBeInTheDocument();
  });
});

describe('LeavePage actions', () => {
  it('submits a leave request with numeric ids', async () => {
    mockAuth(['Leave.Read', 'Leave.Request']);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useLeaveRequests').mockReturnValue({ data: paged([]), isLoading: false, isError: false, isPlaceholderData: false } as any);
    const mutateAsync = vi.fn().mockResolvedValue(req({}));
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useRequestLeave').mockReturnValue({ mutateAsync, isPending: false } as any);

    const user = userEvent.setup();
    renderPage();
    await user.type(screen.getByLabelText('Employee ID'), '5');
    await user.selectOptions(screen.getByLabelText('Type'), '2');
    await user.type(screen.getByLabelText('From'), '2026-07-20');
    await user.type(screen.getByLabelText('To'), '2026-07-22');
    await user.click(screen.getByRole('button', { name: /request leave/i }));

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledOnce());
    const arg = mutateAsync.mock.calls[0][0];
    expect(arg.employeeId).toBe(5);
    expect(typeof arg.leaveTypeId).toBe('number');
  });

  it('shows the 422 over-balance message when approval is rejected', async () => {
    mockAuth(['Leave.Read', 'Leave.Approve']);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useLeaveRequests').mockReturnValue({ data: paged([req({})]), isLoading: false, isError: false, isPlaceholderData: false } as any);
    const mutateAsync = vi.fn().mockRejectedValue(new ApiError(422, 'Insufficient leave balance: requested 3, remaining 2.'));
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useApproveLeave').mockReturnValue({ mutateAsync, isPending: false } as any);

    const user = userEvent.setup();
    renderPage();
    await user.click(screen.getByRole('button', { name: /^approve$/i }));

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledOnce());
    expect(await screen.findByText(/insufficient leave balance/i)).toBeInTheDocument();
  });
});
