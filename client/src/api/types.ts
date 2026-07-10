// API DTOs mirroring the server contract (05). In a fuller build these would be
// generated from OpenAPI (05 §13, 07 §8.2); hand-written here for the P1 slice.

export interface AuthUser {
  id: number;
  userName: string;
  roles: string[];
  permissions: string[];
}

export interface LoginResult {
  accessToken: string;
  tokenType: string;
  expiresIn: number;
  refreshToken: string;
  user: AuthUser;
}

export interface Department {
  id: number;
  code: string;
  name: string;
  parentDepartmentId: number | null;
  isActive: boolean;
}

export interface Employee {
  id: number;
  employeeNo: string;
  firstName: string;
  lastName: string;
  email: string | null;
  primaryDepartmentId: number;
  status: string;
  isActive: boolean;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

// --- Scheduling (P2) ---
export interface Shift {
  id: number;
  code: string;
  name: string;
  startTime: string; // "HH:mm:ss"
  endTime: string;
  breakMinutes: number;
  graceInMinutes: number;
  graceOutMinutes: number;
  overtimeThresholdMinutes: number;
  isOvernight: boolean;
  isActive: boolean;
}

export interface ShiftAssignment {
  id: number;
  shiftId: number;
  employeeId: number | null;
  departmentId: number | null;
  effectiveFrom: string; // date
  effectiveTo: string | null;
}

// --- Attendance (P2) ---
export interface AttendanceException {
  id: number;
  type: string;
  isResolved: boolean;
  notes: string | null;
}

export interface AttendanceRecord {
  id: number;
  employeeId: number;
  workDate: string;
  resolvedShiftId: number | null;
  firstInUtc: string | null;
  lastOutUtc: string | null;
  workedMinutes: number | null;
  lateMinutes: number;
  earlyLeaveMinutes: number;
  overtimeMinutes: number;
  status: string;
  exceptions: AttendanceException[];
  concurrencyToken: string;
}

// --- Devices (P3) ---
export interface Device {
  id: number;
  serialNo: string;
  name: string;
  ipAddress: string | null;
  port: number | null;
  model: string | null;
  isEnabled: boolean;
  lastSeenUtc: string | null;
}

export interface DeviceSyncState {
  deviceId: number;
  lastWatermarkUtc: string | null;
  lastSyncSucceededUtc: string | null;
  consecutiveFailureCount: number;
}

export interface Enrollment {
  id: number;
  employeeId: number;
  deviceId: number;
  deviceUserId: string;
  isActive: boolean;
}

export interface SyncDeviceResult {
  deviceId: number;
  reachable: boolean;
  downloaded: number;
  ingested: number;
  duplicates: number;
  unresolved: number;
  watermarkAdvanced: boolean;
  alerted: boolean;
}

export interface ReconcileResult {
  deviceId: number;
  deviceCount: number;
  storedCount: number;
  missingCount: number;
  extraCount: number;
  clean: boolean;
}

export interface TestConnectionResult {
  reachable: boolean;
  message: string | null;
}

// --- Leave (P4) ---
export interface LeaveType {
  id: number;
  code: string;
  name: string;
  isActive: boolean;
}

export interface LeaveRequest {
  id: number;
  employeeId: number;
  leaveTypeId: number;
  startDate: string;
  endDate: string;
  dayCount: number;
  status: string;
  approverUserId: number | null;
  reason: string | null;
}

export interface LeaveBalance {
  id: number;
  employeeId: number;
  leaveTypeId: number;
  year: number;
  entitledDays: number;
  usedDays: number;
  remainingDays: number;
}

/** RFC 9457 problem details shape (05 §6). */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  correlationId?: string;
  errors?: Record<string, string[]>;
}
