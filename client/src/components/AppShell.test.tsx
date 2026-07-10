import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AppShell } from './AppShell';
import * as AuthModule from '../auth/AuthContext';
import type { AuthUser } from '../api/types';

function renderShellAs(user: AuthUser) {
  vi.spyOn(AuthModule, 'useAuth').mockReturnValue({
    user,
    isAuthenticated: true,
    isInitialising: false,
    hasPermission: (p: string) => user.permissions.includes(p),
    login: vi.fn(),
    logout: vi.fn(),
  });
  return render(
    <MemoryRouter>
      <AppShell><div>content</div></AppShell>
    </MemoryRouter>,
  );
}

describe('AppShell role-filtered navigation (08 §3, 06 §5)', () => {
  it('shows only nav items the user has permission for', () => {
    renderShellAs({
      id: 1, userName: 'manager', roles: ['Manager'],
      permissions: ['Employee.Read', 'Attendance.Read'],
    });

    // Permitted:
    expect(screen.getByRole('link', { name: 'Dashboard' })).toBeInTheDocument(); // always
    expect(screen.getByRole('link', { name: 'Employees' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Attendance' })).toBeInTheDocument();
    // NOT permitted → hidden (UI convenience; server still enforces):
    expect(screen.queryByRole('link', { name: 'Devices' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Departments' })).not.toBeInTheDocument();
  });

  it('shows the full nav for an administrator', () => {
    renderShellAs({
      id: 2, userName: 'admin', roles: ['Administrator'],
      permissions: ['Employee.Read', 'Department.Read', 'Shift.Read', 'Attendance.Read', 'Device.Read'],
    });

    for (const label of ['Dashboard', 'Attendance', 'Shifts', 'Employees', 'Departments', 'Devices']) {
      expect(screen.getByRole('link', { name: label })).toBeInTheDocument();
    }
  });
});
