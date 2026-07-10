import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { EmployeesPage } from './EmployeesPage';
import { ApiError } from '../api/client';
import * as AuthModule from '../auth/AuthContext';
import * as Hooks from '../api/hooks';
import type { Employee, PagedResult } from '../api/types';

function emp(over: Partial<Employee>): Employee {
  return {
    id: 1, employeeNo: 'E1', firstName: 'Ann', lastName: 'Silva', email: null,
    primaryDepartmentId: 1, status: 'Active', isActive: true, ...over,
  };
}
function paged(items: Employee[], over: Partial<PagedResult<Employee>> = {}): PagedResult<Employee> {
  return { items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1, ...over };
}

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}><EmployeesPage /></QueryClientProvider>);
}

function mockAuth(permissions: string[]) {
  vi.spyOn(AuthModule, 'useAuth').mockReturnValue({
    user: { id: 1, userName: 'u', roles: [], permissions },
    isAuthenticated: true, isInitialising: false,
    hasPermission: (p) => permissions.includes(p),
    login: vi.fn(), logout: vi.fn(),
  });
}

beforeEach(() => {
  vi.restoreAllMocks();
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useDepartments').mockReturnValue({ data: [{ id: 1, code: 'ENG', name: 'Engineering', parentDepartmentId: null, isActive: true }], isLoading: false, isError: false } as any);
});

describe('EmployeesPage rendering & permissions', () => {
  it('shows the create form only for users with Employee.Write', () => {
    mockAuth(['Employee.Read']); // read-only
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useEmployees').mockReturnValue({ data: paged([emp({})]), isLoading: false, isError: false, isFetching: false, isPlaceholderData: false } as any);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useCreateEmployee').mockReturnValue({ mutateAsync: vi.fn(), isPending: false } as any);
    renderPage();
    expect(screen.queryByRole('button', { name: /add employee/i })).not.toBeInTheDocument();
    expect(screen.getByText('E1')).toBeInTheDocument();
  });

  it('shows the empty state when there are no employees', () => {
    mockAuth(['Employee.Read', 'Employee.Write']);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useEmployees').mockReturnValue({ data: paged([]), isLoading: false, isError: false, isFetching: false, isPlaceholderData: false } as any);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useCreateEmployee').mockReturnValue({ mutateAsync: vi.fn(), isPending: false } as any);
    renderPage();
    expect(screen.getByText(/no employees/i)).toBeInTheDocument();
  });

  it('surfaces the load-error state', () => {
    mockAuth(['Employee.Read']);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useEmployees').mockReturnValue({ data: undefined, isLoading: false, isError: true, isFetching: false, isPlaceholderData: false } as any);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useCreateEmployee').mockReturnValue({ mutateAsync: vi.fn(), isPending: false } as any);
    renderPage();
    expect(screen.getByRole('alert')).toHaveTextContent(/failed to load/i);
  });
});

describe('EmployeesPage field-error mapping (G4 fix — 08 §9)', () => {
  it('maps PascalCase server field errors onto the camelCase form fields', async () => {
    mockAuth(['Employee.Read', 'Employee.Write']);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useEmployees').mockReturnValue({ data: paged([]), isLoading: false, isError: false, isFetching: false, isPlaceholderData: false } as any);
    const mutateAsync = vi.fn().mockRejectedValue(
      new ApiError(400, 'Validation failed', 'corr-1', { EmployeeNo: ['EmployeeNo already exists.'] }),
    );
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useCreateEmployee').mockReturnValue({ mutateAsync, isPending: false } as any);

    const user = userEvent.setup();
    const { container } = renderPage();
    await user.type(container.querySelector('#employeeNo') as HTMLInputElement, 'E1');
    await user.type(container.querySelector('#firstName') as HTMLInputElement, 'Ann');
    await user.type(container.querySelector('#lastName') as HTMLInputElement, 'Silva');
    await user.selectOptions(container.querySelector('#primaryDepartmentId') as HTMLSelectElement, '1');
    await user.click(screen.getByRole('button', { name: /add employee/i }));

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledOnce());
    // The server's PascalCase "EmployeeNo" error must appear (mapped onto the field).
    expect(await screen.findByText('EmployeeNo already exists.')).toBeInTheDocument();
  });
});
