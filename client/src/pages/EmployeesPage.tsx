import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import {
  useCreateEmployee, useDepartments, useEmployees, useUpdateEmployee, useSetEmployeeActive,
  type CreateEmployeeInput,
} from '../api/hooks';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../api/client';
import { useDebounced } from '../lib/useDebounced';
import {
  Button, Card, Field, Input, Select, PageHeader, DataTable, Th, Td, Tr,
  Toolbar, SearchInput, Pagination, EmptyState, StatusPill, IconButton, ActionIcons, Modal, ConfirmDialog,
  DEFAULT_PAGE_SIZE, useToast,
} from '../components/ui';
import type { Employee } from '../api/types';

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
  const [showForm, setShowForm] = useState(true);
  const [editing, setEditing] = useState<Employee | null>(null);
  const [toggle, setToggle] = useState<Employee | null>(null);
  const debouncedSearch = useDebounced(search);

  const { data: departments } = useDepartments();
  const { data, isLoading, isError, isFetching, isPlaceholderData } = useEmployees(
    page, DEFAULT_PAGE_SIZE, deptFilter ? Number(deptFilter) : undefined, debouncedSearch,
  );
  const createEmployee = useCreateEmployee();
  const setActive = useSetEmployeeActive();
  const { register, handleSubmit, reset, setError, formState } = useForm<CreateEmployeeInput>();
  const [formError, setFormError] = useState<string | null>(null);
  const toast = useToast();

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
            if (field) { setError(field, { type: 'server', message: messages.join(' ') }); mappedAny = true; }
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

  const confirmToggle = async () => {
    if (!toggle) return;
    try {
      await setActive.mutateAsync({ id: toggle.id, active: !toggle.isActive });
      toast(toggle.isActive ? 'Employee deactivated.' : 'Employee activated.');
      setToggle(null);
    } catch (error) {
      toast(error instanceof ApiError ? error.message : 'Action failed.', 'error');
      setToggle(null);
    }
  };

  return (
    <div className="space-y-5">
      <PageHeader
        title="Employees"
        subtitle="Manage your workforce records."
        actions={canWrite ? (
          <Button variant={showForm ? 'secondary' : 'primary'} onClick={() => setShowForm((v) => !v)}>
            {showForm ? 'Hide form' : 'New employee'}
          </Button>
        ) : undefined}
      />

      {canWrite && showForm && (
        <Card>
          <form onSubmit={onSubmit} className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
            <Field id="employeeNo" label="Employee No" required error={formState.errors.employeeNo?.message}>
              <Input id="employeeNo" aria-describedby={formState.errors.employeeNo ? 'employeeNo-err' : undefined} {...register('employeeNo', { required: 'Required' })} />
            </Field>
            <Field id="firstName" label="First name" required error={formState.errors.firstName?.message}>
              <Input id="firstName" aria-describedby={formState.errors.firstName ? 'firstName-err' : undefined} {...register('firstName', { required: 'Required' })} />
            </Field>
            <Field id="lastName" label="Last name" required error={formState.errors.lastName?.message}>
              <Input id="lastName" aria-describedby={formState.errors.lastName ? 'lastName-err' : undefined} {...register('lastName', { required: 'Required' })} />
            </Field>
            <Field id="email" label="Email" error={formState.errors.email?.message}>
              <Input id="email" aria-describedby={formState.errors.email ? 'email-err' : undefined} {...register('email')} />
            </Field>
            <Field id="primaryDepartmentId" label="Department" required error={formState.errors.primaryDepartmentId?.message}>
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
                  <Th module="employees">Department</Th>
                  <Th module="employees">Email</Th>
                  <Th module="employees">Status</Th>
                  {canWrite && <Th module="employees"><span className="sr-only">Actions</span></Th>}
                </tr>
              }
            >
              {data.items.map((e) => (
                <Tr key={e.id}>
                  <Td className="font-mono font-medium text-[var(--color-ink)]">{e.employeeNo}</Td>
                  <Td>{e.firstName} {e.lastName}</Td>
                  <Td>{departments?.find((d) => d.id === e.primaryDepartmentId)?.name ?? `#${e.primaryDepartmentId}`}</Td>
                  <Td className="text-[var(--color-muted)]">{e.email ?? '—'}</Td>
                  <Td>
                    <StatusPill tone={e.isActive ? 'success' : 'neutral'} label={e.isActive ? 'Active' : 'Inactive'} />
                  </Td>
                  {canWrite && (
                    <Td>
                      <div className="flex items-center justify-end gap-1">
                        <IconButton label={`Edit ${e.employeeNo}`} icon={ActionIcons.edit} onClick={() => setEditing(e)} />
                        {e.isActive ? (
                          <IconButton label={`Deactivate ${e.employeeNo}`} tone="danger" icon={ActionIcons.deactivate} onClick={() => setToggle(e)} />
                        ) : (
                          <IconButton label={`Activate ${e.employeeNo}`} tone="success" icon={ActionIcons.activate} onClick={() => setToggle(e)} />
                        )}
                      </div>
                    </Td>
                  )}
                </Tr>
              ))}
            </DataTable>
          )}

          <Pagination page={data.page} totalPages={data.totalPages} totalCount={data.totalCount} onPage={setPage} />
        </div>
      )}

      {editing && (
        <EditEmployeeModal
          employee={editing}
          departments={departments ?? []}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); toast('Employee updated.'); }}
        />
      )}

      {toggle && (
        <ConfirmDialog
          title={toggle.isActive ? 'Deactivate employee?' : 'Activate employee?'}
          tone={toggle.isActive ? 'danger' : 'primary'}
          confirmLabel={toggle.isActive ? 'Deactivate' : 'Activate'}
          loading={setActive.isPending}
          message={
            toggle.isActive
              ? <>Deactivate <strong>{toggle.firstName} {toggle.lastName}</strong> ({toggle.employeeNo})? They will no longer be counted as active.</>
              : <>Reactivate <strong>{toggle.firstName} {toggle.lastName}</strong> ({toggle.employeeNo})?</>
          }
          onConfirm={confirmToggle}
          onCancel={() => setToggle(null)}
        />
      )}
    </div>
  );
}

interface EditForm { firstName: string; lastName: string; email?: string; primaryDepartmentId: number }

function EditEmployeeModal({
  employee, departments, onClose, onSaved,
}: {
  employee: Employee;
  departments: { id: number; name: string }[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const update = useUpdateEmployee();
  const { register, handleSubmit, formState, reset } = useForm<EditForm>({
    defaultValues: {
      firstName: employee.firstName, lastName: employee.lastName,
      email: employee.email ?? '', primaryDepartmentId: employee.primaryDepartmentId,
    },
  });
  const [error, setError] = useState<string | null>(null);
  useEffect(() => reset({
    firstName: employee.firstName, lastName: employee.lastName,
    email: employee.email ?? '', primaryDepartmentId: employee.primaryDepartmentId,
  }), [employee, reset]);

  const submit = handleSubmit(async (v) => {
    setError(null);
    try {
      await update.mutateAsync({ id: employee.id, ...v, primaryDepartmentId: Number(v.primaryDepartmentId) });
      onSaved();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Update failed.');
    }
  });

  return (
    <Modal
      title={`Edit ${employee.employeeNo}`}
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose}>Cancel</Button>
          <Button variant="primary" loading={update.isPending} onClick={submit}>Save changes</Button>
        </>
      }
    >
      <form onSubmit={submit} className="grid grid-cols-2 gap-3">
        <Field id="editFirst" label="First name" required error={formState.errors.firstName?.message}>
          <Input id="editFirst" {...register('firstName', { required: 'Required' })} />
        </Field>
        <Field id="editLast" label="Last name" required error={formState.errors.lastName?.message}>
          <Input id="editLast" {...register('lastName', { required: 'Required' })} />
        </Field>
        <Field id="editEmail" label="Email" className="col-span-2">
          <Input id="editEmail" {...register('email')} />
        </Field>
        <Field id="editDept" label="Department" required className="col-span-2">
          <Select id="editDept" {...register('primaryDepartmentId', { required: 'Required' })}>
            {departments.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
          </Select>
        </Field>
        {error && <p role="alert" className="col-span-2 text-sm font-medium text-[var(--color-danger)]">{error}</p>}
      </form>
    </Modal>
  );
}
