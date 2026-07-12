import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { UsersPage } from './UsersPage';
import * as Hooks from '../api/hooks';
import { ToastProvider } from '../components/ui';
import type { AdminUser } from '../api/types';

function user(over: Partial<AdminUser>): AdminUser {
  return {
    id: 1, userName: 'alice', email: 'alice@example.com', roles: ['Employee'],
    isActive: true, employeeId: 10, lastLoginUtc: null, ...over,
  };
}

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <ToastProvider>
        <UsersPage />
      </ToastProvider>
    </QueryClientProvider>,
  );
}

const q = (data: unknown, over: Record<string, unknown> = {}) =>
  ({ data, isLoading: false, isError: false, ...over });

beforeEach(() => {
  vi.restoreAllMocks();
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useRoles').mockReturnValue(q([{ name: 'Employee', description: null }, { name: 'Administrator', description: null }]) as any);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useEmployees').mockReturnValue(q({ items: [], page: 1, pageSize: 100, totalCount: 0, totalPages: 0 }) as any);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useSetUserActive').mockReturnValue({ mutateAsync: vi.fn(), isPending: false } as any);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useCreateUser').mockReturnValue({ mutateAsync: vi.fn(), isPending: false } as any);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  vi.spyOn(Hooks, 'useUpdateUser').mockReturnValue({ mutateAsync: vi.fn(), isPending: false } as any);
});

describe('UsersPage', () => {
  it('renders the user list with roles and status', () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useUsers').mockReturnValue(q([user({ userName: 'alice', roles: ['Employee'] })]) as any);
    renderPage();

    expect(screen.getByText('alice')).toBeInTheDocument();
    expect(screen.getByText('Employee')).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();
  });

  it('deactivating a user asks for confirmation before calling the mutation', async () => {
    const mutateAsync = vi.fn().mockResolvedValue({});
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useSetUserActive').mockReturnValue({ mutateAsync, isPending: false } as any);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useUsers').mockReturnValue(q([user({ id: 7, userName: 'bob', isActive: true })]) as any);
    renderPage();

    // Open the row actions menu, then choose Deactivate.
    await userEvent.click(screen.getByRole('button', { name: /row actions/i }));
    await userEvent.click(await screen.findByText('Deactivate'));

    // Nothing fires until the confirmation is accepted.
    expect(mutateAsync).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Deactivate' }));

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledWith({ id: 7, active: false }));
  });

  it('opens the New user modal with the required fields', async () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.spyOn(Hooks, 'useUsers').mockReturnValue(q([]) as any);
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: 'New user' }));

    expect(await screen.findByLabelText('Username')).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toBeInTheDocument();
    expect(screen.getByLabelText('Password')).toBeInTheDocument();
  });
});
