import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import {
  useCreateUser, useEmployees, useRoles, useSetUserActive, useUpdateUser, useUsers,
  type CreateUserInput,
} from '../api/hooks';
import { ApiError } from '../api/client';
import {
  AsyncView, Button, ConfirmDialog, DataTable, Field, Input, Modal, Select,
  PageHeader, StatusPill, Td, Th, Tr, RowActions, useToast,
} from '../components/ui';
import type { AdminUser, Employee } from '../api/types';

function fmtDate(iso: string | null): string {
  return iso ? new Date(iso).toLocaleString() : 'never';
}

export function UsersPage() {
  const users = useUsers();
  const roles = useRoles();
  const employees = useEmployees(1, 100);
  const setActive = useSetUserActive();
  const toast = useToast();

  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<AdminUser | null>(null);
  const [toggle, setToggle] = useState<AdminUser | null>(null);

  const roleNames = (roles.data ?? []).map((r) => r.name);
  const employeeList = employees.data?.items ?? [];

  const confirmToggle = async () => {
    if (!toggle) return;
    try {
      await setActive.mutateAsync({ id: toggle.id, active: !toggle.isActive });
      toast(toggle.isActive ? 'User deactivated.' : 'User activated.');
      setToggle(null);
    } catch (error) {
      toast(error instanceof ApiError ? error.message : 'Action failed.', 'error');
      setToggle(null);
    }
  };

  return (
    <div className="space-y-5">
      <PageHeader
        title="Users"
        subtitle="Create login accounts and assign roles."
        actions={<Button variant="primary" onClick={() => setCreating(true)}>New user</Button>}
      />

      <AsyncView isLoading={users.isLoading} isError={users.isError} isEmpty={users.data?.length === 0} emptyText="No users yet.">
        {users.data && (
          <DataTable
            head={
              <tr>
                <Th module="employees">Username</Th>
                <Th module="employees">Email</Th>
                <Th module="employees">Roles</Th>
                <Th module="employees">Status</Th>
                <Th module="employees">Last login</Th>
                <Th module="employees"><span className="sr-only">Actions</span></Th>
              </tr>
            }
          >
            {users.data.map((u) => (
              <Tr key={u.id}>
                <Td className="font-medium text-[var(--color-ink)]">{u.userName}</Td>
                <Td className="text-[var(--color-muted)]">{u.email}</Td>
                <Td>
                  <div className="flex flex-wrap gap-1">
                    {u.roles.map((r) => (
                      <span key={r} className="rounded-full bg-[var(--color-brand-50)] px-2 py-0.5 text-xs font-medium text-[var(--color-brand-700)]">{r}</span>
                    ))}
                  </div>
                </Td>
                <Td><StatusPill tone={u.isActive ? 'success' : 'neutral'} label={u.isActive ? 'Active' : 'Inactive'} /></Td>
                <Td className="text-[var(--color-muted)]">{fmtDate(u.lastLoginUtc)}</Td>
                <Td className="text-right">
                  <RowActions
                    actions={[
                      { label: 'Edit', onClick: () => setEditing(u) },
                      u.isActive
                        ? { label: 'Deactivate', tone: 'danger', onClick: () => setToggle(u) }
                        : { label: 'Activate', onClick: () => setToggle(u) },
                    ]}
                  />
                </Td>
              </Tr>
            ))}
          </DataTable>
        )}
      </AsyncView>

      {creating && <CreateUserModal roles={roleNames} employees={employeeList} onClose={() => setCreating(false)} onSaved={() => { setCreating(false); toast('User created.'); }} />}
      {editing && <EditUserModal user={editing} roles={roleNames} employees={employeeList} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); toast('User updated.'); }} />}
      {toggle && (
        <ConfirmDialog
          title={toggle.isActive ? 'Deactivate user?' : 'Activate user?'}
          tone={toggle.isActive ? 'danger' : 'primary'}
          confirmLabel={toggle.isActive ? 'Deactivate' : 'Activate'}
          loading={setActive.isPending}
          message={
            toggle.isActive
              ? <>Deactivate <strong>{toggle.userName}</strong>? They will no longer be able to sign in.</>
              : <>Reactivate <strong>{toggle.userName}</strong>? They will be able to sign in again.</>
          }
          onConfirm={confirmToggle}
          onCancel={() => setToggle(null)}
        />
      )}
    </div>
  );
}

/** A multi-select list of role checkboxes. */
function RoleChecks({ roles, selected, onToggle }: { roles: string[]; selected: string[]; onToggle: (r: string) => void }) {
  return (
    <div className="flex flex-wrap gap-2">
      {roles.map((r) => {
        const on = selected.includes(r);
        return (
          <button
            key={r}
            type="button"
            aria-pressed={on}
            onClick={() => onToggle(r)}
            className={`rounded-full px-3 py-1 text-sm font-medium ring-1 ring-inset transition-colors ${
              on
                ? 'bg-[var(--color-brand-600)] text-white ring-[var(--color-brand-600)]'
                : 'bg-white text-[var(--color-ink-soft)] ring-[var(--color-line)] hover:bg-[var(--color-surface-2)]'
            }`}
          >
            {r}
          </button>
        );
      })}
    </div>
  );
}

interface CreateForm { userName: string; email: string; password: string; employeeId: string }

function CreateUserModal({ roles, employees, onClose, onSaved }: { roles: string[]; employees: Employee[]; onClose: () => void; onSaved: () => void }) {
  const create = useCreateUser();
  const { register, handleSubmit, formState } = useForm<CreateForm>();
  const [selectedRoles, setSelectedRoles] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);

  const toggleRole = (r: string) => setSelectedRoles((cur) => cur.includes(r) ? cur.filter((x) => x !== r) : [...cur, r]);

  const submit = handleSubmit(async (v) => {
    setError(null);
    if (selectedRoles.length === 0) { setError('Select at least one role.'); return; }
    try {
      await create.mutateAsync({
        userName: v.userName, email: v.email, password: v.password, roles: selectedRoles,
        employeeId: v.employeeId ? Number(v.employeeId) : null,
      } as CreateUserInput);
      onSaved();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Failed to create user.');
    }
  });

  return (
    <Modal
      title="New user"
      onClose={onClose}
      footer={<><Button onClick={onClose}>Cancel</Button><Button variant="primary" loading={create.isPending} onClick={submit}>Create user</Button></>}
    >
      <form onSubmit={submit} className="space-y-3">
        <Field id="u-name" label="Username" required error={formState.errors.userName?.message}>
          <Input id="u-name" {...register('userName', { required: 'Required' })} />
        </Field>
        <Field id="u-email" label="Email" required error={formState.errors.email?.message}>
          <Input id="u-email" type="email" {...register('email', { required: 'Required' })} />
        </Field>
        <Field id="u-pass" label="Password" required hint="At least 8 characters." error={formState.errors.password?.message}>
          <Input id="u-pass" type="password" {...register('password', { required: 'Required', minLength: { value: 8, message: 'At least 8 characters.' } })} />
        </Field>
        <div>
          <span className="mb-1 block text-sm font-medium text-[var(--color-ink-soft)]">Roles <span className="text-[var(--color-danger)]">*</span></span>
          <RoleChecks roles={roles} selected={selectedRoles} onToggle={toggleRole} />
        </div>
        <Field id="u-emp" label="Link to employee" hint="So this user can see their own attendance & leave.">
          <Select id="u-emp" {...register('employeeId')}>
            <option value="">Not linked</option>
            {employees.map((e) => <option key={e.id} value={e.id}>{e.employeeNo} — {e.firstName} {e.lastName}</option>)}
          </Select>
        </Field>
        {error && <p role="alert" className="text-sm font-medium text-[var(--color-danger)]">{error}</p>}
      </form>
    </Modal>
  );
}

function EditUserModal({ user, roles, employees, onClose, onSaved }: { user: AdminUser; roles: string[]; employees: Employee[]; onClose: () => void; onSaved: () => void }) {
  const update = useUpdateUser();
  const { register, handleSubmit, reset, formState } = useForm<{ email: string; newPassword?: string; employeeId: string }>({
    defaultValues: { email: user.email, newPassword: '', employeeId: user.employeeId?.toString() ?? '' },
  });
  const [selectedRoles, setSelectedRoles] = useState<string[]>(user.roles);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    reset({ email: user.email, newPassword: '', employeeId: user.employeeId?.toString() ?? '' });
    setSelectedRoles(user.roles);
  }, [user, reset]);

  const toggleRole = (r: string) => setSelectedRoles((cur) => cur.includes(r) ? cur.filter((x) => x !== r) : [...cur, r]);

  const submit = handleSubmit(async (v) => {
    setError(null);
    if (selectedRoles.length === 0) { setError('Select at least one role.'); return; }
    try {
      await update.mutateAsync({
        id: user.id, email: v.email, roles: selectedRoles, newPassword: v.newPassword || undefined,
        employeeId: v.employeeId ? Number(v.employeeId) : null,
      });
      onSaved();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Failed to update user.');
    }
  });

  return (
    <Modal
      title={`Edit ${user.userName}`}
      onClose={onClose}
      footer={<><Button onClick={onClose}>Cancel</Button><Button variant="primary" loading={update.isPending} onClick={submit}>Save changes</Button></>}
    >
      <form onSubmit={submit} className="space-y-3">
        <Field id="eu-email" label="Email" required error={formState.errors.email?.message}>
          <Input id="eu-email" type="email" {...register('email', { required: 'Required' })} />
        </Field>
        <div>
          <span className="mb-1 block text-sm font-medium text-[var(--color-ink-soft)]">Roles <span className="text-[var(--color-danger)]">*</span></span>
          <RoleChecks roles={roles} selected={selectedRoles} onToggle={toggleRole} />
        </div>
        <Field id="eu-emp" label="Link to employee" hint="So this user can see their own attendance & leave.">
          <Select id="eu-emp" {...register('employeeId')}>
            <option value="">Not linked</option>
            {employees.map((e) => <option key={e.id} value={e.id}>{e.employeeNo} — {e.firstName} {e.lastName}</option>)}
          </Select>
        </Field>
        <Field id="eu-pass" label="Reset password" hint="Leave blank to keep the current password.">
          <Input id="eu-pass" type="password" autoComplete="new-password" {...register('newPassword')} />
        </Field>
        {error && <p role="alert" className="text-sm font-medium text-[var(--color-danger)]">{error}</p>}
      </form>
    </Modal>
  );
}
