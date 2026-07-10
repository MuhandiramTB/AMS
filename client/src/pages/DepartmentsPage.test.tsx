import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { DepartmentsPage } from './DepartmentsPage';
import { ApiError } from '../api/client';
import * as AuthModule from '../auth/AuthContext';
import * as Hooks from '../api/hooks';
import type { Department } from '../api/types';

function dept(over: Partial<Department>): Department {
  return { id: 1, code: 'ENG', name: 'Engineering', parentDepartmentId: null, isActive: true, ...over };
}

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}><DepartmentsPage /></QueryClientProvider>);
}

function mockAuth(permissions: string[]) {
  vi.spyOn(AuthModule, 'useAuth').mockReturnValue({
    user: { id: 1, userName: 'u', roles: [], permissions },
    isAuthenticated: true, isInitialising: false,
    hasPermission: (p) => permissions.includes(p),
    login: vi.fn(), logout: vi.fn(),
  });
}

beforeEach(() => vi.restoreAllMocks());

describe('DepartmentsPage', () => {
  it('renders departments with active/inactive status by label', () => {
    mockAuth(['Department.Read']);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useDepartments').mockReturnValue({ data: [dept({}), dept({ id: 2, code: 'OLD', name: 'Legacy', isActive: false })], isLoading: false, isError: false } as any);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useCreateDepartment').mockReturnValue({ mutateAsync: vi.fn(), isPending: false } as any);
    renderPage();
    expect(screen.getByText('ENG')).toBeInTheDocument();
    expect(screen.getByText(/● Active/)).toBeInTheDocument();
    expect(screen.getByText(/○ Inactive/)).toBeInTheDocument();
  });

  it('hides the create form for read-only users', () => {
    mockAuth(['Department.Read']);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useDepartments').mockReturnValue({ data: [dept({})], isLoading: false, isError: false } as any);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useCreateDepartment').mockReturnValue({ mutateAsync: vi.fn(), isPending: false } as any);
    renderPage();
    expect(screen.queryByRole('button', { name: /add department/i })).not.toBeInTheDocument();
  });

  it('shows the 409 conflict message when a duplicate code is submitted', async () => {
    mockAuth(['Department.Read', 'Department.Write']);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useDepartments').mockReturnValue({ data: [], isLoading: false, isError: false } as any);
    const mutateAsync = vi.fn().mockRejectedValue(new ApiError(409, "A department with code 'ENG' already exists."));
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useCreateDepartment').mockReturnValue({ mutateAsync, isPending: false } as any);

    const user = userEvent.setup();
    renderPage();
    await user.type(screen.getByLabelText('Code'), 'ENG');
    await user.type(screen.getByLabelText('Name'), 'Engineering');
    await user.click(screen.getByRole('button', { name: /add department/i }));

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledOnce());
    expect(await screen.findByText(/already exists/i)).toBeInTheDocument();
  });
});
