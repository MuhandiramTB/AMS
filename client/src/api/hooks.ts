import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient, toApiError } from './client';
import type {
  AttendanceRecord,
  AttendanceSummary,
  Department,
  Device,
  DeviceSyncState,
  Employee,
  Enrollment,
  LeaveBalance,
  LeaveRequest,
  LeaveType,
  PagedResult,
  ReconcileResult,
  Shift,
  SyncDeviceResult,
  TestConnectionResult,
} from './types';

// React Query owns all server state — caching, retries, invalidation (07 §9).

export function useDepartments() {
  return useQuery({
    queryKey: ['departments'],
    queryFn: async () => {
      const { data } = await apiClient.get<Department[]>('/departments');
      return data;
    },
  });
}

export function useCreateDepartment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: { code: string; name: string }) => {
      try {
        const { data } = await apiClient.post<Department>('/departments', input);
        return data;
      } catch (error) {
        throw toApiError(error);
      }
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['departments'] }),
  });
}

export function useUpdateDepartment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, name, parentDepartmentId }: { id: number; name: string; parentDepartmentId?: number | null }) => {
      try {
        return (await apiClient.put<Department>(`/departments/${id}`, { name, parentDepartmentId })).data;
      } catch (error) {
        throw toApiError(error);
      }
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['departments'] }),
  });
}

export function useSetDepartmentActive() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, active }: { id: number; active: boolean }) => {
      try {
        return (await apiClient.post<Department>(`/departments/${id}/${active ? 'activate' : 'deactivate'}`, {})).data;
      } catch (error) {
        throw toApiError(error);
      }
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['departments'] }),
  });
}

export function useEmployees(page: number, pageSize: number, departmentId?: number, search?: string) {
  const q = search?.trim() || undefined;
  return useQuery({
    queryKey: ['employees', page, pageSize, departmentId ?? null, q ?? null],
    queryFn: async () => {
      const { data } = await apiClient.get<PagedResult<Employee>>('/employees', {
        // Server-side search maps to the API's `q` param (06/05 §10.2).
        params: { page, pageSize, departmentId, q },
      });
      return data;
    },
    // Keep showing the previous page's rows while the next page loads (08 §7).
    placeholderData: keepPreviousData,
  });
}

export interface CreateEmployeeInput {
  employeeNo: string;
  firstName: string;
  lastName: string;
  email?: string;
  primaryDepartmentId: number;
}

export function useCreateEmployee() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: CreateEmployeeInput) => {
      try {
        const { data } = await apiClient.post<Employee>('/employees', input);
        return data;
      } catch (error) {
        throw toApiError(error);
      }
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['employees'] }),
  });
}

export interface UpdateEmployeeInput {
  id: number;
  firstName: string;
  lastName: string;
  email?: string;
  primaryDepartmentId: number;
}

export function useUpdateEmployee() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, ...body }: UpdateEmployeeInput) => {
      try {
        return (await apiClient.put<Employee>(`/employees/${id}`, body)).data;
      } catch (error) {
        throw toApiError(error);
      }
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['employees'] }),
  });
}

export function useSetEmployeeActive() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, active }: { id: number; active: boolean }) => {
      try {
        return (await apiClient.post<Employee>(`/employees/${id}/${active ? 'activate' : 'deactivate'}`, {})).data;
      } catch (error) {
        throw toApiError(error);
      }
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['employees'] }),
  });
}

// --- Shifts (P2) ---
export function useShifts() {
  return useQuery({
    queryKey: ['shifts'],
    queryFn: async () => (await apiClient.get<Shift[]>('/shifts')).data,
  });
}

export interface CreateShiftInput {
  code: string;
  name: string;
  startTime: string;
  endTime: string;
  breakMinutes: number;
  graceInMinutes: number;
  graceOutMinutes: number;
  overtimeThresholdMinutes: number;
}

export function useCreateShift() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (input: CreateShiftInput) => {
      try {
        return (await apiClient.post<Shift>('/shifts', input)).data;
      } catch (error) {
        throw toApiError(error);
      }
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['shifts'] }),
  });
}

export interface UpdateShiftInput extends CreateShiftInput {
  id: number;
}

export function useUpdateShift() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, code: _code, ...body }: UpdateShiftInput) => {
      try {
        // Code is immutable on update; send only the editable rule values.
        return (await apiClient.put<Shift>(`/shifts/${id}`, body)).data;
      } catch (error) {
        throw toApiError(error);
      }
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['shifts'] }),
  });
}

export function useSetShiftActive() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, active }: { id: number; active: boolean }) => {
      try {
        return (await apiClient.post<Shift>(`/shifts/${id}/${active ? 'activate' : 'deactivate'}`, {})).data;
      } catch (error) {
        throw toApiError(error);
      }
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['shifts'] }),
  });
}

export interface AssignShiftInput {
  shiftId: number;
  employeeId?: number | null;
  departmentId?: number | null;
  effectiveFrom: string;
  effectiveTo?: string | null;
}

export function useAssignShift() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (input: AssignShiftInput) => {
      try {
        return (await apiClient.post('/shifts/assignments', input)).data;
      } catch (error) {
        throw toApiError(error);
      }
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['shifts'] }),
  });
}

// --- Attendance (P2) ---
export function useAttendanceRecords(
  page: number,
  pageSize: number,
  filters: { employeeId?: number; fromDate?: string; toDate?: string } = {},
) {
  return useQuery({
    queryKey: ['attendance', page, pageSize, filters],
    queryFn: async () =>
      (await apiClient.get<PagedResult<AttendanceRecord>>('/attendance/records', {
        params: { page, pageSize, ...filters },
      })).data,
    placeholderData: keepPreviousData,
  });
}

export function useAttendanceRecord(id: number | null) {
  return useQuery({
    queryKey: ['attendance-record', id],
    enabled: id !== null,
    queryFn: async () =>
      (await apiClient.get<AttendanceRecord>(`/attendance/records/${id}`)).data,
  });
}

export interface CorrectAttendanceInput {
  id: number;
  firstInUtc?: string | null;
  lastOutUtc?: string | null;
  reason: string;
  ifMatch: string; // ETag concurrency token (05 §8.2)
}

export function useCorrectAttendance() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (input: CorrectAttendanceInput) => {
      try {
        const { data } = await apiClient.patch<AttendanceRecord>(
          `/attendance/records/${input.id}`,
          { firstInUtc: input.firstInUtc, lastOutUtc: input.lastOutUtc, reason: input.reason },
          { headers: { 'If-Match': `"${input.ifMatch}"` } },
        );
        return data;
      } catch (error) {
        throw toApiError(error);
      }
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['attendance'] }),
  });
}

// --- Devices (P3) ---
export function useDevices() {
  return useQuery({
    queryKey: ['devices'],
    queryFn: async () => (await apiClient.get<Device[]>('/devices')).data,
    // Device health should feel live; refetch periodically (08 §8.4, NFR-03).
    refetchInterval: 15000,
  });
}

export function useDeviceSyncState(deviceId: number | null) {
  return useQuery({
    queryKey: ['device-sync-state', deviceId],
    enabled: deviceId !== null,
    queryFn: async () =>
      (await apiClient.get<DeviceSyncState>(`/devices/${deviceId}/sync-state`)).data,
  });
}

export interface RegisterDeviceInput {
  serialNo: string;
  name: string;
  ipAddress?: string | null;
  port?: number | null;
  model?: string | null;
}

export function useRegisterDevice() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (input: RegisterDeviceInput) => {
      try {
        return (await apiClient.post<Device>('/devices', input)).data;
      } catch (error) {
        throw toApiError(error);
      }
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['devices'] }),
  });
}

/** Device action hook factory (sync-now, test-connection, enable, disable, reconcile). */
function useDeviceAction<T>(path: (id: number) => string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (deviceId: number) => {
      try {
        return (await apiClient.post<T>(path(deviceId))).data;
      } catch (error) {
        throw toApiError(error);
      }
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['devices'] }),
  });
}

export const useSyncDevice = () => useDeviceAction<SyncDeviceResult>((id) => `/devices/${id}/sync-now`);
export const useTestDevice = () => useDeviceAction<TestConnectionResult>((id) => `/devices/${id}/test-connection`);
export const useReconcileDevice = () => useDeviceAction<ReconcileResult>((id) => `/devices/${id}/reconcile`);
export const useEnableDevice = () => useDeviceAction<Device>((id) => `/devices/${id}/enable`);
export const useDisableDevice = () => useDeviceAction<Device>((id) => `/devices/${id}/disable`);

export function useDeviceEnrollments(deviceId: number | null) {
  return useQuery({
    queryKey: ['device-enrollments', deviceId],
    enabled: deviceId !== null,
    queryFn: async () =>
      (await apiClient.get<Enrollment[]>(`/devices/${deviceId}/enrollments`)).data,
  });
}

export function useEnrollEmployee() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (input: { deviceId: number; employeeId: number; deviceUserId: string }) => {
      try {
        return (await apiClient.post<Enrollment>(
          `/devices/${input.deviceId}/enrollments`,
          { employeeId: input.employeeId, deviceUserId: input.deviceUserId },
        )).data;
      } catch (error) {
        throw toApiError(error);
      }
    },
    onSuccess: (_data, vars) =>
      qc.invalidateQueries({ queryKey: ['device-enrollments', vars.deviceId] }),
  });
}

// --- Leave (P4) ---
export function useLeaveTypes() {
  return useQuery({
    queryKey: ['leave-types'],
    queryFn: async () => (await apiClient.get<LeaveType[]>('/leave/types')).data,
  });
}

export function useLeaveBalances(employeeId: number | null, year: number) {
  return useQuery({
    queryKey: ['leave-balances', employeeId, year],
    enabled: employeeId !== null,
    queryFn: async () =>
      (await apiClient.get<LeaveBalance[]>('/leave/balances', { params: { employeeId, year } })).data,
  });
}

export function useLeaveRequests(
  page: number,
  pageSize: number,
  filters: { employeeId?: number; status?: string } = {},
) {
  return useQuery({
    queryKey: ['leave-requests', page, pageSize, filters],
    queryFn: async () =>
      (await apiClient.get<PagedResult<LeaveRequest>>('/leave/requests', {
        params: { page, pageSize, ...filters },
      })).data,
    placeholderData: keepPreviousData,
  });
}

export interface RequestLeaveInput {
  employeeId: number;
  leaveTypeId: number;
  startDate: string;
  endDate: string;
  reason?: string;
}

export function useRequestLeave() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (input: RequestLeaveInput) => {
      try {
        return (await apiClient.post<LeaveRequest>('/leave/requests', input)).data;
      } catch (error) {
        throw toApiError(error);
      }
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['leave-requests'] }),
  });
}

/** Leave decision hook factory (approve/reject/cancel), invalidating requests + balances. */
function useLeaveDecision(path: (id: number) => string, body?: () => unknown) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      try {
        return (await apiClient.post<LeaveRequest>(path(id), body ? body() : {})).data;
      } catch (error) {
        throw toApiError(error);
      }
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['leave-requests'] });
      qc.invalidateQueries({ queryKey: ['leave-balances'] });
    },
  });
}

export const useApproveLeave = () =>
  useLeaveDecision((id) => `/leave/requests/${id}/approve`, () => ({ allowOverride: false }));
export const useRejectLeave = () => useLeaveDecision((id) => `/leave/requests/${id}/reject`);
export const useCancelLeave = () => useLeaveDecision((id) => `/leave/requests/${id}/cancel`);

// --- Reporting / dashboards (P5) ---
export function useAttendanceSummary(
  workDate?: string,
  departmentId?: number,
  options: { enabled?: boolean } = {},
) {
  const enabled = options.enabled ?? true;
  return useQuery({
    queryKey: ['attendance-summary', workDate ?? null, departmentId ?? null],
    enabled,
    queryFn: async () =>
      (await apiClient.get<AttendanceSummary>('/dashboards/attendance-summary', {
        params: { workDate, departmentId },
      })).data,
    // The dashboard should feel near-live without hammering the API (FR-RPT-001).
    refetchInterval: enabled ? 30000 : false,
  });
}
