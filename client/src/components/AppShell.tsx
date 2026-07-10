import type { ReactNode } from 'react';
import { NavLink } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

interface NavItem {
  label: string;
  to: string;
  permission?: string; // if set, only shown when the user has it (08 §3)
}

const NAV: NavItem[] = [
  { label: 'Dashboard', to: '/' },
  { label: 'Attendance', to: '/attendance', permission: 'Attendance.Read' },
  { label: 'Leave', to: '/leave', permission: 'Leave.Read' },
  { label: 'Shifts', to: '/shifts', permission: 'Shift.Read' },
  { label: 'Employees', to: '/employees', permission: 'Employee.Read' },
  { label: 'Departments', to: '/departments', permission: 'Department.Read' },
  { label: 'Devices', to: '/devices', permission: 'Device.Read' },
];

export function AppShell({ children }: { children: ReactNode }) {
  const { user, logout, hasPermission } = useAuth();

  // Nav is filtered by permission — the UI never advertises what the user can't
  // do; the server still enforces authorization (08 §3, 06 §5).
  const visible = NAV.filter((item) => !item.permission || hasPermission(item.permission));

  return (
    <div className="flex h-screen flex-col">
      {/* Skip link — first focusable element, lets keyboard users bypass the nav
          and jump straight to page content. (WCAG 2.4.1 Bypass Blocks.) */}
      <a
        href="#main-content"
        className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-20 focus:rounded focus:bg-blue-600 focus:px-3 focus:py-2 focus:text-white"
      >
        Skip to content
      </a>
      <header className="flex items-center justify-between bg-slate-800 px-4 py-3 text-white">
        <span className="text-lg font-semibold">TAMS</span>
        <div className="flex items-center gap-3 text-sm">
          <span>
            {user?.userName}
            {user?.roles.length ? ` (${user.roles.join(', ')})` : ''}
          </span>
          <button
            onClick={logout}
            className="rounded bg-slate-600 px-3 py-1 hover:bg-slate-500"
          >
            Log out
          </button>
        </div>
      </header>

      <div className="flex flex-1 overflow-hidden">
        <nav aria-label="Primary" className="w-56 shrink-0 bg-slate-100 p-3">
          <ul className="space-y-1">
            {visible.map((item) => (
              <li key={item.to}>
                <NavLink
                  to={item.to}
                  end={item.to === '/'}
                  className={({ isActive }) =>
                    `block rounded px-3 py-2 text-sm ${
                      isActive
                        ? 'bg-blue-600 text-white'
                        : 'text-slate-700 hover:bg-slate-200'
                    }`
                  }
                >
                  {item.label}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>

        <main id="main-content" tabIndex={-1} className="flex-1 overflow-auto bg-white p-6">{children}</main>
      </div>
    </div>
  );
}
