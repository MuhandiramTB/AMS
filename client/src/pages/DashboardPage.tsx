import { useAuth } from '../auth/AuthContext';
import { useAttendanceSummary } from '../api/hooks';
import { AsyncView, StatusPill } from '../components/ui';

/** Tile tone reflects whether the metric warrants attention (08 §12 — never colour alone). */
type Tile = { label: string; value: number; tone: 'success' | 'warning' | 'danger' | 'neutral' };

export function DashboardPage() {
  const { user, hasPermission } = useAuth();
  const canViewReports = hasPermission('Report.Read');

  // Default to today (server also defaults to today when workDate is omitted).
  // Only fetch when the user is permitted, so restricted users never trigger a 403.
  const summary = useAttendanceSummary(undefined, undefined, { enabled: canViewReports });

  return (
    <div>
      <h1 className="mb-1 text-2xl font-semibold text-slate-800">Dashboard</h1>
      <p className="mb-6 text-slate-600">Welcome, {user?.userName}.</p>

      {!canViewReports ? (
        <p className="py-4 text-slate-500">
          You don’t have access to attendance reporting. Contact an administrator if you
          believe this is a mistake.
        </p>
      ) : (
        <AsyncView isLoading={summary.isLoading} isError={summary.isError}>
          {summary.data && (
            <>
              <div className="mb-4 flex items-center gap-2 text-sm text-slate-500">
                <span>Attendance for {summary.data.workDate}</span>
                {summary.data.openExceptions > 0 && (
                  <StatusPill tone="warning" label={`${summary.data.openExceptions} open exceptions`} />
                )}
              </div>

              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
                {tilesFor(summary.data).map((tile) => (
                  <SummaryTile key={tile.label} {...tile} />
                ))}
              </div>
            </>
          )}
        </AsyncView>
      )}
    </div>
  );
}

function tilesFor(s: {
  present: number;
  absent: number;
  late: number;
  onLeave: number;
  openExceptions: number;
}): Tile[] {
  return [
    { label: 'Present', value: s.present, tone: 'success' },
    { label: 'Absent', value: s.absent, tone: s.absent > 0 ? 'danger' : 'neutral' },
    { label: 'Late', value: s.late, tone: s.late > 0 ? 'warning' : 'neutral' },
    { label: 'On leave', value: s.onLeave, tone: 'neutral' },
  ];
}

const VALUE_TONES: Record<Tile['tone'], string> = {
  success: 'text-green-700',
  warning: 'text-amber-700',
  danger: 'text-red-700',
  neutral: 'text-slate-800',
};

function SummaryTile({ label, value, tone }: Tile) {
  return (
    <div className="rounded-lg border border-slate-200 p-4 shadow-sm">
      <div className="text-sm text-slate-500">{label}</div>
      <div className={`mt-1 text-2xl font-semibold ${VALUE_TONES[tone]}`}>{value}</div>
    </div>
  );
}
