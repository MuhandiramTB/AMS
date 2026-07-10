import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render } from '@testing-library/react';
import { axe } from 'vitest-axe';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import * as AuthModule from './auth/AuthContext';
import * as Hooks from './api/hooks';
import { LoginPage } from './pages/LoginPage';
import { EmployeesPage } from './pages/EmployeesPage';
import { AppShell } from './components/AppShell';

// vitest-axe exposes toHaveNoViolations via its matchers; assert on results directly
// to avoid global matcher registration friction.
async function expectNoViolations(container: HTMLElement) {
  // color-contrast requires a real layout/canvas engine (unavailable under jsdom);
  // it is covered by the manual WCAG pass and Playwright, not here.
  const results = await axe(container, { rules: { 'color-contrast': { enabled: false } } });
  expect(results.violations).toEqual([]);
}

function withProviders(ui: React.ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return (
    <QueryClientProvider client={qc}>
      <MemoryRouter>{ui}</MemoryRouter>
    </QueryClientProvider>
  );
}

function mockAuth(permissions: string[]) {
  vi.spyOn(AuthModule, 'useAuth').mockReturnValue({
    user: { id: 1, userName: 'alice', roles: ['HROfficer'], permissions },
    isAuthenticated: true, isInitialising: false,
    hasPermission: (p) => permissions.includes(p),
    login: vi.fn(), logout: vi.fn(),
  });
}

beforeEach(() => vi.restoreAllMocks());

describe('accessibility (WCAG) smoke — axe', () => {
  it('LoginPage has no violations', async () => {
    vi.spyOn(AuthModule, 'useAuth').mockReturnValue({
      user: null, isAuthenticated: false, isInitialising: false,
      hasPermission: () => false, login: vi.fn(), logout: vi.fn(),
    });
    const { container } = render(withProviders(<LoginPage />));
    await expectNoViolations(container);
  });

  it('EmployeesPage create form has labelled controls (no violations)', async () => {
    mockAuth(['Employee.Read', 'Employee.Write']);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useEmployees').mockReturnValue({ data: { items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 1 }, isLoading: false, isError: false, isFetching: false, isPlaceholderData: false } as any);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useDepartments').mockReturnValue({ data: [{ id: 1, code: 'ENG', name: 'Engineering', parentDepartmentId: null, isActive: true }] } as any);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useCreateEmployee').mockReturnValue({ mutateAsync: vi.fn(), isPending: false } as any);
    const { container } = render(withProviders(<EmployeesPage />));
    await expectNoViolations(container);
  });

  it('AppShell (skip link, primary nav, main landmark) has no violations', async () => {
    mockAuth(['Attendance.Read', 'Leave.Read']);
    const { container } = render(withProviders(<AppShell><h1>Page</h1></AppShell>));
    await expectNoViolations(container);
  });
});
