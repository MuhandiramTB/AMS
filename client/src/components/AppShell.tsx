import { useEffect, useRef, useState, type ReactNode } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
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
  users: <><circle cx="9" cy="8" r="3.2" /><path d="M3.5 20a5.5 5.5 0 0111 0M17 8h4M19 6v4" strokeLinecap="round" /></>,
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
  { label: 'Users', to: '/users', permission: 'User.Manage', icon: <NavIcon path={I.users} /> },
];

function initials(name?: string) {
  if (!name) return '?';
  return name.slice(0, 2).toUpperCase();
}

export function AppShell({ children }: { children: ReactNode }) {
  const { user, logout, hasPermission } = useAuth();
  const location = useLocation();
  const [mobileOpen, setMobileOpen] = useState(false);

  // Nav is filtered by permission — the UI never advertises what the user can't
  // do; the server still enforces authorization (08 §3, 06 §5).
  const visible = NAV.filter((item) => !item.permission || hasPermission(item.permission));

  // Close the mobile drawer whenever the route changes or Escape is pressed.
  useEffect(() => { setMobileOpen(false); }, [location.pathname]);
  useEffect(() => {
    if (!mobileOpen) return;
    function onKey(e: KeyboardEvent) { if (e.key === 'Escape') setMobileOpen(false); }
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [mobileOpen]);

  return (
    <div className="flex h-screen overflow-hidden bg-[var(--color-surface-2)]">
      {/* Skip link — first focusable element (WCAG 2.4.1 Bypass Blocks). */}
      <a
        href="#main-content"
        className="sr-only focus:not-sr-only focus:absolute focus:left-3 focus:top-3 focus:z-30 focus:rounded-[var(--radius-md)] focus:bg-[var(--color-brand-600)] focus:px-3 focus:py-2 focus:text-sm focus:font-semibold focus:text-white"
      >
        Skip to content
      </a>

      {/* Desktop sidebar — always visible from md up. */}
      <aside className="hidden w-64 shrink-0 flex-col border-r border-[var(--color-line)] bg-[var(--color-surface)] md:flex">
        <SidebarBody items={visible} onLogout={logout} />
      </aside>

      {/* Mobile drawer — rendered only when opened, so it never duplicates the nav. */}
      {mobileOpen && (
        <div className="fixed inset-0 z-40 md:hidden">
          <div className="absolute inset-0 bg-[var(--color-ink)]/40" onClick={() => setMobileOpen(false)} aria-hidden="true" />
          <aside className="animate-rise absolute inset-y-0 left-0 flex w-72 max-w-[85%] flex-col border-r border-[var(--color-line)] bg-[var(--color-surface)] shadow-[var(--shadow-pop)]">
            <SidebarBody items={visible} onLogout={logout} onClose={() => setMobileOpen(false)} />
          </aside>
        </div>
      )}

      {/* Main column */}
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex items-center justify-between gap-3 border-b border-[var(--color-line)] bg-[var(--color-surface)]/90 px-4 py-3 backdrop-blur md:px-6">
          {/* Hamburger — mobile only; opens the nav drawer. */}
          <button
            onClick={() => setMobileOpen(true)}
            aria-label="Open menu"
            aria-expanded={mobileOpen}
            className="grid h-9 w-9 place-items-center rounded-[var(--radius-md)] text-[var(--color-ink-soft)] ring-1 ring-inset ring-[var(--color-line)] transition-colors hover:bg-[var(--color-surface-3)] md:hidden"
          >
            <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><path d="M4 6h16M4 12h16M4 18h16" strokeLinecap="round" /></svg>
          </button>
          <span className="text-sm font-bold text-[var(--color-ink)] md:hidden">TAMS</span>
          <div className="flex-1" />
          <ProfileMenu userName={user?.userName} roles={user?.roles ?? []} onLogout={logout} />
        </header>

        <main id="main-content" tabIndex={-1} className="flex-1 overflow-auto px-4 py-6 md:px-8 md:py-8">
          <div className="mx-auto max-w-6xl">{children}</div>
        </main>
      </div>
    </div>
  );
}

/** The sidebar contents (brand + nav + sign out), shared by the desktop sidebar
 *  and the mobile drawer. `onClose` (mobile) closes the drawer when a link is tapped. */
function SidebarBody({ items, onLogout, onClose }: { items: NavItem[]; onLogout: () => void; onClose?: () => void }) {
  return (
    <>
      <div className="flex items-center justify-between px-5 py-5">
        <div className="flex items-center gap-2.5">
          <span className="grid h-9 w-9 place-items-center rounded-[var(--radius-md)] bg-[var(--color-brand-600)] text-white shadow-sm" aria-hidden="true">
            <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="2"><circle cx="12" cy="12" r="8" /><path d="M12 8v4l2.5 1.5" strokeLinecap="round" /></svg>
          </span>
          <div className="leading-tight">
            <div className="text-sm font-bold tracking-tight text-[var(--color-ink)]">TAMS</div>
            <div className="text-[0.68rem] text-[var(--color-muted-soft)]">Time &amp; Attendance</div>
          </div>
        </div>
        {onClose && (
          <button onClick={onClose} aria-label="Close menu" className="rounded-[var(--radius-md)] p-1 text-[var(--color-muted-soft)] hover:bg-[var(--color-surface-3)] hover:text-[var(--color-ink)] md:hidden">
            <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><path d="M18 6 6 18M6 6l12 12" strokeLinecap="round" /></svg>
          </button>
        )}
      </div>

      <nav aria-label="Primary" className="flex-1 overflow-y-auto px-3 py-2">
        <p className="px-3 pb-1.5 pt-2 text-[0.65rem] font-semibold uppercase tracking-wider text-[var(--color-muted-soft)]">Menu</p>
        <ul className="space-y-0.5">
          {items.map((item) => (
            <li key={item.to}>
              <NavLink
                to={item.to}
                end={item.to === '/'}
                onClick={onClose}
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

      <div className="border-t border-[var(--color-line)] p-3">
        <button
          onClick={onLogout}
          className="flex w-full items-center gap-3 rounded-[var(--radius-md)] px-3 py-2 text-sm font-medium text-[var(--color-ink-soft)] transition-colors hover:bg-[var(--color-absent-bg)] hover:text-[var(--color-danger)]"
        >
          <svg viewBox="0 0 24 24" className="h-[18px] w-[18px] shrink-0" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true"><path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4M16 17l5-5-5-5M21 12H9" strokeLinecap="round" strokeLinejoin="round" /></svg>
          Sign out
        </button>
      </div>
    </>
  );
}

/** Header profile icon that opens a small dropdown (identity + sign out). */
function ProfileMenu({ userName, roles, onLogout }: { userName?: string; roles: string[]; onLogout: () => void }) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    function onDoc(e: MouseEvent) { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false); }
    function onKey(e: KeyboardEvent) { if (e.key === 'Escape') setOpen(false); }
    document.addEventListener('mousedown', onDoc);
    document.addEventListener('keydown', onKey);
    return () => { document.removeEventListener('mousedown', onDoc); document.removeEventListener('keydown', onKey); };
  }, [open]);

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label="Account menu"
        className="grid h-9 w-9 place-items-center rounded-full bg-[var(--color-brand-100)] text-sm font-bold text-[var(--color-brand-700)] ring-1 ring-inset ring-[var(--color-brand-200)] transition-shadow hover:shadow-[var(--shadow-card)]"
      >
        {initials(userName)}
      </button>
      {open && (
        <div role="menu" className="absolute right-0 z-30 mt-2 w-56 overflow-hidden rounded-[var(--radius-lg)] border border-[var(--color-line)] bg-[var(--color-surface)] shadow-[var(--shadow-pop)]">
          <div className="border-b border-[var(--color-line-soft)] px-4 py-3">
            <div className="text-sm font-semibold text-[var(--color-ink)]">{userName}</div>
            {roles.length > 0 && <div className="mt-0.5 text-xs text-[var(--color-muted)]">{roles.join(', ')}</div>}
          </div>
          <button
            role="menuitem"
            onClick={() => { setOpen(false); onLogout(); }}
            className="flex w-full items-center gap-2 px-4 py-2.5 text-left text-sm font-medium text-[var(--color-ink-soft)] transition-colors hover:bg-[var(--color-absent-bg)] hover:text-[var(--color-danger)]"
          >
            <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true"><path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4M16 17l5-5-5-5M21 12H9" strokeLinecap="round" strokeLinejoin="round" /></svg>
            Sign out
          </button>
        </div>
      )}
    </div>
  );
}
