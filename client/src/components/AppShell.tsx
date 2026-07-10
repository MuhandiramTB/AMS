import type { ReactNode } from 'react';
import { NavLink } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

interface NavItem {
  label: string;
  to: string;
  permission?: string; // if set, only shown when the user has it (08 §3)
  icon: ReactNode;
}

// Inline stroke icons (no icon dependency — keeps the bundle lean).
const I = {
  dashboard: <path d="M3 3h7v9H3zM14 3h7v5h-7zM14 12h7v9h-7zM3 15h7v6H3z" />,
  attendance: <><circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" strokeLinecap="round" /></>,
  leave: <><rect x="3" y="4" width="18" height="17" rx="2" /><path d="M3 9h18M8 2v4M16 2v4" strokeLinecap="round" /></>,
  shifts: <><path d="M12 3a9 9 0 100 18 9 9 0 000-18z" /><path d="M12 8v4l2 2" strokeLinecap="round" /></>,
  employees: <><circle cx="9" cy="8" r="3.2" /><path d="M3.5 20a5.5 5.5 0 0111 0M16 11a3 3 0 100-6M15.5 20a5.5 5.5 0 015-3" strokeLinecap="round" /></>,
  departments: <><path d="M3 21h18M6 21V8l6-4 6 4v13M9 12h.01M15 12h.01M9 16h.01M15 16h.01" strokeLinecap="round" /></>,
  devices: <><rect x="4" y="3" width="16" height="14" rx="2" /><path d="M8 21h8M12 17v4" strokeLinecap="round" /></>,
};

function NavIcon({ path }: { path: ReactNode }) {
  return (
    <svg viewBox="0 0 24 24" className="h-[18px] w-[18px] shrink-0" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true">
      {path}
    </svg>
  );
}

const NAV: NavItem[] = [
  { label: 'Dashboard', to: '/', icon: <NavIcon path={I.dashboard} /> },
  { label: 'Attendance', to: '/attendance', permission: 'Attendance.Read', icon: <NavIcon path={I.attendance} /> },
  { label: 'Leave', to: '/leave', permission: 'Leave.Read', icon: <NavIcon path={I.leave} /> },
  { label: 'Shifts', to: '/shifts', permission: 'Shift.Read', icon: <NavIcon path={I.shifts} /> },
  { label: 'Employees', to: '/employees', permission: 'Employee.Read', icon: <NavIcon path={I.employees} /> },
  { label: 'Departments', to: '/departments', permission: 'Department.Read', icon: <NavIcon path={I.departments} /> },
  { label: 'Devices', to: '/devices', permission: 'Device.Read', icon: <NavIcon path={I.devices} /> },
];

function initials(name?: string) {
  if (!name) return '?';
  return name.slice(0, 2).toUpperCase();
}

export function AppShell({ children }: { children: ReactNode }) {
  const { user, logout, hasPermission } = useAuth();

  // Nav is filtered by permission — the UI never advertises what the user can't
  // do; the server still enforces authorization (08 §3, 06 §5).
  const visible = NAV.filter((item) => !item.permission || hasPermission(item.permission));

  return (
    <div className="flex h-screen overflow-hidden bg-[var(--color-surface-2)]">
      {/* Skip link — first focusable element (WCAG 2.4.1 Bypass Blocks). */}
      <a
        href="#main-content"
        className="sr-only focus:not-sr-only focus:absolute focus:left-3 focus:top-3 focus:z-30 focus:rounded-[var(--radius-md)] focus:bg-[var(--color-brand-600)] focus:px-3 focus:py-2 focus:text-sm focus:font-semibold focus:text-white"
      >
        Skip to content
      </a>

      {/* Sidebar */}
      <aside className="hidden w-64 shrink-0 flex-col border-r border-[var(--color-line)] bg-[var(--color-surface)] md:flex">
        {/* Brand lockup */}
        <div className="flex items-center gap-2.5 px-5 py-5">
          <span className="grid h-9 w-9 place-items-center rounded-[var(--radius-md)] bg-[var(--color-brand-600)] text-white shadow-sm" aria-hidden="true">
            <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="2"><circle cx="12" cy="12" r="8" /><path d="M12 8v4l2.5 1.5" strokeLinecap="round" /></svg>
          </span>
          <div className="leading-tight">
            <div className="text-sm font-bold tracking-tight text-[var(--color-ink)]">TAMS</div>
            <div className="text-[0.68rem] text-[var(--color-muted-soft)]">Time &amp; Attendance</div>
          </div>
        </div>

        <nav aria-label="Primary" className="flex-1 overflow-y-auto px-3 py-2">
          <p className="px-3 pb-1.5 pt-2 text-[0.65rem] font-semibold uppercase tracking-wider text-[var(--color-muted-soft)]">Menu</p>
          <ul className="space-y-0.5">
            {visible.map((item) => (
              <li key={item.to}>
                <NavLink
                  to={item.to}
                  end={item.to === '/'}
                  className={({ isActive }) =>
                    `group flex items-center gap-3 rounded-[var(--radius-md)] px-3 py-2 text-sm font-medium transition-colors ${
                      isActive
                        ? 'bg-[var(--color-brand-50)] text-[var(--color-brand-700)]'
                        : 'text-[var(--color-ink-soft)] hover:bg-[var(--color-surface-3)] hover:text-[var(--color-ink)]'
                    }`
                  }
                >
                  {({ isActive }) => (
                    <>
                      <span className={isActive ? 'text-[var(--color-brand-600)]' : 'text-[var(--color-muted-soft)] group-hover:text-[var(--color-muted)]'}>
                        {item.icon}
                      </span>
                      {item.label}
                    </>
                  )}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>

        <div className="border-t border-[var(--color-line)] p-3 text-[0.68rem] text-[var(--color-muted-soft)]">
          Enterprise Time &amp; Attendance
        </div>
      </aside>

      {/* Main column */}
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex items-center justify-between gap-4 border-b border-[var(--color-line)] bg-[var(--color-surface)]/90 px-4 py-3 backdrop-blur md:px-6">
          {/* Mobile brand (sidebar hidden on small screens) */}
          <span className="text-sm font-bold text-[var(--color-ink)] md:hidden">TAMS</span>
          <div className="flex-1" />
          <div className="flex items-center gap-3">
            <div className="hidden text-right sm:block">
              <div className="text-sm font-semibold text-[var(--color-ink)]">{user?.userName}</div>
              {user?.roles.length ? <div className="text-xs text-[var(--color-muted)]">{user.roles.join(', ')}</div> : null}
            </div>
            <span className="grid h-9 w-9 place-items-center rounded-full bg-[var(--color-brand-100)] text-sm font-bold text-[var(--color-brand-700)]" aria-hidden="true">
              {initials(user?.userName)}
            </span>
            <button
              onClick={logout}
              className="rounded-[var(--radius-md)] px-3 py-1.5 text-sm font-semibold text-[var(--color-muted)] ring-1 ring-inset ring-[var(--color-line)] transition-colors hover:bg-[var(--color-surface-3)] hover:text-[var(--color-ink)]"
            >
              Log out
            </button>
          </div>
        </header>

        <main id="main-content" tabIndex={-1} className="flex-1 overflow-auto px-4 py-6 md:px-8 md:py-8">
          <div className="mx-auto max-w-6xl">{children}</div>
        </main>
      </div>
    </div>
  );
}
