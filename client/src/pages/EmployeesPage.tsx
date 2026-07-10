import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useCreateEmployee, useDepartments, useEmployees, type CreateEmployeeInput } from '../api/hooks';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../api/client';
import { useDebounced } from '../lib/useDebounced';
import {
  Button, Card, Field, Input, Select, PageHeader, DataTable, Th, Td, Tr,
  Toolbar, SearchInput, Pagination, EmptyState, DEFAULT_PAGE_SIZE, useToast,
} from '../components/ui';

const FIELD_NAMES: (keyof CreateEmployeeInput)[] = ['employeeNo', 'firstName', 'lastName', 'email', 'primaryDepartmentId'];

/** Match a server field key (often PascalCase) to a form field (camelCase). */
function toFormField(serverKey: string): keyof CreateEmployeeInput | null {
  const lower = serverKey.toLowerCase();
  return FIELD_NAMES.find((f) => f.toLowerCase() === lower) ?? null;
}

export function EmployeesPage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission('Employee.Write');

  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [deptFilter, setDeptFilter] = useState('');
  const debouncedSearch = useDebounced(search);

  const { data: departments } = useDepartments();
  const { data, isLoading, isError, isFetching, isPlaceholderData } = useEmployees(
    page, DEFAULT_PAGE_SIZE, deptFilter ? Number(deptFilter) : undefined, debouncedSearch,
  );
  const createEmployee = useCreateEmployee();
  const { register, handleSubmit, reset, setError, formState } = useForm<CreateEmployeeInput>();
  const [formError, setFormError] = useState<string | null>(null);
  const toast = useToast();

  // Any filter change returns to page 1.
  const changeSearch = (v: string) => { setSearch(v); setPage(1); };
  const changeDept = (v: string) => { setDeptFilter(v); setPage(1); };

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await createEmployee.mutateAsync({ ...values, primaryDepartmentId: Number(values.primaryDepartmentId) });
      reset();
      toast('Employee added.');
    } catch (error) {
      if (error instanceof ApiError) {
        if (error.fieldErrors) {
          let mappedAny = false;
          for (const [key, messages] of Object.entries(error.fieldErrors)) {
            const field = toFormField(key);
            if (field) {
              setError(field, { type: 'server', message: messages.join(' ') });
              mappedAny = true;
            }
          }
          if (!mappedAny) setFormError(Object.values(error.fieldErrors).flat().join(' '));
        } else {
          setFormError(error.message);
        }
      } else {
        setFormError('An unexpected error occurred.');
      }
    }
  });

  return (
    <div className="space-y-5">
      <PageHeader title="Employees" subtitle="Manage your workforce records." />

      {canWrite && (
        <Card>
          <form onSubmit={onSubmit} className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
            <Field id="employeeNo" label="Employee No" error={formState.errors.employeeNo?.message}>
              <Input id="employeeNo" aria-describedby={formState.errors.employeeNo ? 'employeeNo-err' : undefined} {...register('employeeNo', { required: 'Required' })} />
            </Field>
            <Field id="firstName" label="First name" error={formState.errors.firstName?.message}>
              <Input id="firstName" aria-describedby={formState.errors.firstName ? 'firstName-err' : undefined} {...register('firstName', { required: 'Required' })} />
            </Field>
            <Field id="lastName" label="Last name" error={formState.errors.lastName?.message}>
              <Input id="lastName" aria-describedby={formState.errors.lastName ? 'lastName-err' : undefined} {...register('lastName', { required: 'Required' })} />
            </Field>
            <Field id="email" label="Email" error={formState.errors.email?.message}>
              <Input id="email" aria-describedby={formState.errors.email ? 'email-err' : undefined} {...register('email')} />
            </Field>
            <Field id="primaryDepartmentId" label="Department" error={formState.errors.primaryDepartmentId?.message}>
              <Select id="primaryDepartmentId" aria-describedby={formState.errors.primaryDepartmentId ? 'primaryDepartmentId-err' : undefined} {...register('primaryDepartmentId', { required: 'Required' })}>
                <option value="">Department…</option>
                {departments?.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
              </Select>
            </Field>
            <div className="col-span-2 flex items-center gap-3 sm:col-span-3 lg:col-span-5">
              <Button type="submit" variant="primary" loading={createEmployee.isPending}>
                {createEmployee.isPending ? 'Adding…' : 'Add employee'}
              </Button>
              {formError && <span role="alert" className="text-sm font-medium text-[var(--color-danger)]">{formError}</span>}
            </div>
          </form>
        </Card>
      )}

      {/* Filter section */}
      <Toolbar>
        <SearchInput value={search} onChange={changeSearch} label="Search employees" placeholder="Name or employee no…" />
        <Field id="empDeptFilter" label="Filter by department" className="min-w-48">
          <Select id="empDeptFilter" value={deptFilter} onChange={(e) => changeDept(e.target.value)}>
            <option value="">All departments</option>
            {departments?.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
          </Select>
        </Field>
        <div role="status" aria-live="polite" className="pb-2 text-xs text-[var(--color-muted-soft)]">
          {isFetching && isPlaceholderData ? 'Refreshing…' : ''}
        </div>
      </Toolbar>

      {isError && <p role="alert" className="rounded-[var(--radius-md)] bg-[var(--color-absent-bg)] px-4 py-3 text-sm font-medium text-[var(--color-absent)]">Failed to load employees.</p>}

      {isLoading && (
        <div role="status" aria-live="polite" className="space-y-2">
          <span className="sr-only">Loading…</span>
          {[0, 1, 2, 3].map((i) => <div key={i} className="skeleton h-11 w-full" />)}
        </div>
      )}

      {data && (
        <div className="space-y-3">
          {data.items.length === 0 ? (
            <EmptyState title="No employees yet." hint={search || deptFilter ? 'Try clearing the search or filter.' : canWrite ? 'Add your first employee using the form above.' : undefined} />
          ) : (
            <DataTable
              className={isPlaceholderData ? 'opacity-60' : ''}
              head={
                <tr>
                  <Th module="employees">Employee No</Th>
                  <Th module="employees">Name</Th>
                  <Th module="employees">Email</Th>
                  <Th module="employees">Status</Th>
                </tr>
              }
            >
              {data.items.map((e) => (
                <Tr key={e.id}>
                  <Td className="font-mono font-medium text-[var(--color-ink)]">{e.employeeNo}</Td>
                  <Td>{e.firstName} {e.lastName}</Td>
                  <Td className="text-[var(--color-muted)]">{e.email ?? '—'}</Td>
                  <Td>{e.status}</Td>
                </Tr>
              ))}
            </DataTable>
          )}

          <Pagination page={data.page} totalPages={data.totalPages} totalCount={data.totalCount} onPage={setPage} />
        </div>
      )}
    </div>
  );
}
