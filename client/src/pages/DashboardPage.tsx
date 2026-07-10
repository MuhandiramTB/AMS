import { useAuth } from '../auth/AuthContext';
import { useAttendanceSummary } from '../api/hooks';
import type { AttendanceSummary } from '../api/types';
import { AsyncView, Card, StatTile, StatusPill, PageHeader } from '../components/ui';

// Small inline icons for the KPI tiles.
const icons = {
  present: <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="2"><path d="M20 6L9 17l-5-5" strokeLinecap="round" strokeLinejoin="round" /></svg>,
  absent: <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="2"><path d="M18 6L6 18M6 6l12 12" strokeLinecap="round" /></svg>,
  late: <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="2"><circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" strokeLinecap="round" /></svg>,
  leave: <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="2"><rect x="3" y="4" width="18" height="17" rx="2" /><path d="M3 9h18M8 2v4M16 2v4" strokeLinecap="round" /></svg>,
  exception: <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="2"><path d="M12 9v4M12 17h.01M10.3 3.9L2.4 18a2 2 0 001.7 3h15.8a2 2 0 001.7-3L13.7 3.9a2 2 0 00-3.4 0z" strokeLinecap="round" strokeLinejoin="round" /></svg>,
};

export function DashboardPage() {
  const { user, hasPermission } = useAuth();
  const canViewReports = hasPermission('Report.Read');

  // Default to today (server also defaults to today when workDate is omitted).
  // Only fetch when the user is permitted, so restricted users never trigger a 403.
  const summary = useAttendanceSummary(undefined, undefined, { enabled: canViewReports });

  return (
    <div>
      <PageHeader title={`Welcome back, ${user?.userName ?? ''}`.trim()} subtitle="Here's today's attendance at a glance." />

      {!canViewReports ? (
        <Card>
          <p className="text-sm text-[var(--color-muted)]">
            You don’t have access to attendance reporting. Contact an administrator if you
            believe this is a mistake.
          </p>
        </Card>
      ) : (
        <AsyncView isLoading={summary.isLoading} isError={summary.isError}>
          {summary.data && <DashboardContent data={summary.data} refreshing={summary.isFetching} />}
        </AsyncView>
      )}
    </div>
  );
}

function DashboardContent({ data, refreshing }: { data: AttendanceSummary; refreshing: boolean }) {
  const tiles = [
    { label: 'Present', value: data.present, tone: 'success' as const, icon: icons.present },
    { label: 'Absent', value: data.absent, tone: data.absent > 0 ? ('danger' as const) : ('neutral' as const), icon: icons.absent },
    { label: 'Late', value: data.late, tone: data.late > 0 ? ('warning' as const) : ('neutral' as const), icon: icons.late },
    { label: 'On leave', value: data.onLeave, tone: 'info' as const, icon: icons.leave },
    { label: 'Open exceptions', value: data.openExceptions, tone: data.openExceptions > 0 ? ('warning' as const) : ('neutral' as const), icon: icons.exception },
  ];

  return (
    <div className="space-y-6">
      {/* Context line: date + exceptions + refreshing */}
      <div className="flex flex-wrap items-center gap-3 text-sm text-[var(--color-muted)]">
        <span className="inline-flex items-center gap-1.5">
          <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true"><rect x="3" y="4" width="18" height="17" rx="2" /><path d="M3 9h18M8 2v4M16 2v4" strokeLinecap="round" /></svg>
          Attendance for {data.workDate}
        </span>
        {data.openExceptions > 0 && <StatusPill tone="warning" label={`${data.openExceptions} open exceptions`} />}
        {refreshing && <span role="status" aria-live="polite" className="text-xs text-[var(--color-muted-soft)]">Refreshing…</span>}
      </div>

      {/* KPI stat cards */}
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-5">
        {tiles.map((t, i) => (
          <StatTile key={t.label} label={t.label} value={t.value} tone={t.tone} icon={t.icon} delay={i * 60} />
        ))}
      </div>

      {/* Attendance composition bar */}
      <Card>
        <h2 className="mb-3 text-sm font-semibold text-[var(--color-ink)]">Today's composition</h2>
        <CompositionBar present={data.present} late={data.late} absent={data.absent} onLeave={data.onLeave} />
      </Card>

      {/* Department breakdown */}
      <Card pad={false}>
        <div className="flex items-center justify-between px-5 py-4">
          <h2 className="text-sm font-semibold text-[var(--color-ink)]">By department</h2>
          <span className="text-xs text-[var(--color-muted-soft)]">{data.byDepartment.length} department(s)</span>
        </div>
        {data.byDepartment.length === 0 ? (
          <p className="px-5 pb-5 text-sm text-[var(--color-muted)]">No department activity recorded for this date yet.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-left text-sm">
              <thead>
                <tr className="border-t border-[var(--color-line)] text-[var(--color-muted)]">
                  <th scope="col" className="px-5 py-2.5 text-xs font-semibold uppercase tracking-wide">Department</th>
                  <th scope="col" className="px-5 py-2.5 text-right text-xs font-semibold uppercase tracking-wide tabular">Present</th>
                  <th scope="col" className="px-5 py-2.5 text-right text-xs font-semibold uppercase tracking-wide tabular">Late</th>
                  <th scope="col" className="px-5 py-2.5 text-right text-xs font-semibold uppercase tracking-wide tabular">Absent</th>
                  <th scope="col" className="px-5 py-2.5 text-right text-xs font-semibold uppercase tracking-wide tabular">On leave</th>
                </tr>
              </thead>
              <tbody>
                {data.byDepartment.map((d) => (
                  <tr key={d.departmentId} className="border-t border-[var(--color-line-soft)]">
                    <td className="px-5 py-2.5 font-medium text-[var(--color-ink-soft)]">Dept #{d.departmentId}</td>
                    <td className="px-5 py-2.5 text-right tabular text-[var(--color-present)]">{d.present}</td>
                    <td className="px-5 py-2.5 text-right tabular text-[var(--color-late)]">{d.late}</td>
                    <td className="px-5 py-2.5 text-right tabular text-[var(--color-absent)]">{d.absent}</td>
                    <td className="px-5 py-2.5 text-right tabular text-[var(--color-leave)]">{d.onLeave}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </div>
  );
}

function CompositionBar({ present, late, absent, onLeave }: { present: number; late: number; absent: number; onLeave: number }) {
  const total = Math.max(present + late + absent + onLeave, 1);
  const segs = [
    { label: 'Present', value: present, color: 'var(--color-present)' },
    { label: 'Late', value: late, color: 'var(--color-late)' },
    { label: 'Absent', value: absent, color: 'var(--color-absent)' },
    { label: 'On leave', value: onLeave, color: 'var(--color-leave)' },
  ].filter((s) => s.value > 0);

  return (
    <div>
      <div className="flex h-3 w-full overflow-hidden rounded-full bg-[var(--color-surface-3)]" role="img" aria-label={`Present ${present}, late ${late}, absent ${absent}, on leave ${onLeave}`}>
        {segs.map((s) => (
          <div key={s.label} style={{ width: `${(s.value / total) * 100}%`, background: s.color }} className="h-full first:rounded-l-full last:rounded-r-full" />
        ))}
      </div>
      <div className="mt-3 flex flex-wrap gap-x-5 gap-y-1.5 text-xs text-[var(--color-muted)]">
        {[
          { label: 'Present', value: present, color: 'var(--color-present)' },
          { label: 'Late', value: late, color: 'var(--color-late)' },
          { label: 'Absent', value: absent, color: 'var(--color-absent)' },
          { label: 'On leave', value: onLeave, color: 'var(--color-leave)' },
        ].map((s) => (
          // One combined text node ("Present · 42") so neither the label nor the
          // value collides with the KPI tiles that tests query by exact text.
          <span key={s.label} className="inline-flex items-center gap-1.5">
            <span className="h-2.5 w-2.5 rounded-full" style={{ background: s.color }} aria-hidden="true" />
            <span className="tabular">{`${s.label} · ${s.value}`}</span>
          </span>
        ))}
      </div>
    </div>
  );
}
