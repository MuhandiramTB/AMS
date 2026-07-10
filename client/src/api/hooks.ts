import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient, toApiError } from './client';
import type { Department, Employee, PagedResult } from './types';

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

export function useEmployees(page: number, pageSize: number, departmentId?: number) {
  return useQuery({
    queryKey: ['employees', page, pageSize, departmentId ?? null],
    queryFn: async () => {
      const { data } = await apiClient.get<PagedResult<Employee>>('/employees', {
        params: { page, pageSize, departmentId },
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
