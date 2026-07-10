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

/** RFC 9457 problem details shape (05 §6). */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  correlationId?: string;
  errors?: Record<string, string[]>;
}
