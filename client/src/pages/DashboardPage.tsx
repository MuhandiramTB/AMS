import { useAuth } from '../auth/AuthContext';

export function DashboardPage() {
  const { user } = useAuth();

  return (
    <div>
      <h1 className="mb-4 text-2xl font-semibold text-slate-800">Dashboard</h1>
      <p className="mb-6 text-slate-600">
        Welcome, {user?.userName}. This is the Phase 1 foundation shell. Attendance
        summary tiles and charts arrive in Phase 5 (Reporting).
      </p>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {['Present', 'Absent', 'Late', 'Open exceptions'].map((label) => (
          <div key={label} className="rounded-lg border border-slate-200 p-4 shadow-sm">
            <div className="text-sm text-slate-500">{label}</div>
            <div className="mt-1 text-2xl font-semibold text-slate-400">—</div>
          </div>
        ))}
      </div>
    </div>
  );
}
