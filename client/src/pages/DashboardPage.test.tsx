import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { DashboardPage } from './DashboardPage';
import * as AuthModule from '../auth/AuthContext';
import * as Hooks from '../api/hooks';
import type { AttendanceSummary } from '../api/types';

function summary(over: Partial<AttendanceSummary> = {}): AttendanceSummary {
  return {
    workDate: '2026-07-09',
    present: 42,
    late: 3,
    earlyLeave: 1,
    absent: 5,
    onLeave: 2,
    openExceptions: 4,
    byDepartment: [],
    ...over,
  };
}

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}><DashboardPage /></QueryClientProvider>);
}

function mockAuth(permissions: string[]) {
  vi.spyOn(AuthModule, 'useAuth').mockReturnValue({
    user: { id: 1, userName: 'alice', roles: [], permissions },
    isAuthenticated: true, isInitialising: false,
    hasPermission: (p) => permissions.includes(p),
    login: vi.fn(), logout: vi.fn(),
  });
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function mockSummary(value: Partial<ReturnType<typeof Hooks.useAttendanceSummary>>) {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useAttendanceSummary').mockReturnValue(value as any);
}

beforeEach(() => vi.restoreAllMocks());

describe('DashboardPage', () => {
  it('shows the summary tiles for a user who can view reports', () => {
    mockAuth(['Report.Read']);
    mockSummary({ data: summary({ present: 42, absent: 5, late: 3, onLeave: 2 }), isLoading: false, isError: false });
    renderPage();

    expect(screen.getByText('Present')).toBeInTheDocument();
    expect(screen.getByText('42')).toBeInTheDocument();
    expect(screen.getByText('On leave')).toBeInTheDocument();
    expect(screen.getByText(/Attendance for 2026-07-09/)).toBeInTheDocument();
  });

  it('surfaces open exceptions as a warning pill', () => {
    mockAuth(['Report.Read']);
    mockSummary({ data: summary({ openExceptions: 4 }), isLoading: false, isError: false });
    renderPage();
    expect(screen.getByText(/4 open exceptions/)).toBeInTheDocument();
  });

  it('does not render the exceptions pill when there are none', () => {
    mockAuth(['Report.Read']);
    mockSummary({ data: summary({ openExceptions: 0 }), isLoading: false, isError: false });
    renderPage();
    expect(screen.queryByText(/open exceptions/)).not.toBeInTheDocument();
  });

  it('hides the summary and does not call the endpoint for users without Report.Read', () => {
    mockAuth(['Attendance.Read']);
    const hook = vi.spyOn(Hooks, 'useAttendanceSummary');
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    hook.mockReturnValue({ data: undefined, isLoading: false, isError: false } as any);
    renderPage();

    expect(screen.getByText(/don’t have access to attendance reporting/)).toBeInTheDocument();
    expect(screen.queryByText('Present')).not.toBeInTheDocument();
  });

  it('shows an error state when the summary fails to load', () => {
    mockAuth(['Report.Read']);
    mockSummary({ data: undefined, isLoading: false, isError: true });
    renderPage();
    expect(screen.getByRole('alert')).toHaveTextContent(/Failed to load/);
  });
});
