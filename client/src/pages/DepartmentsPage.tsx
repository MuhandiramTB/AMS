import { useState } from 'react';
import { useForm } from 'react-hook-form';
import {
  useCreateDepartment,
  useDepartments,
  useSetDepartmentActive,
  useUpdateDepartment,
} from '../api/hooks';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../api/client';
import type { Department } from '../api/types';
import {
  AsyncView,
  Button,
  Card,
  ConfirmDialog,
  DataTable,
  EmptyState,
  Field,
  Input,
  Modal,
  PageHeader,
  RowActions,
  SearchInput,
  StatusPill,
  Td,
  Th,
  Toolbar,
  Tr,
  useToast,
} from '../components/ui';

function messageFor(error: unknown): string {
  return error instanceof ApiError ? error.message : 'An unexpected error occurred.';
}

interface DeptForm {
  code: string;
  name: string;
}

export function DepartmentsPage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission('Department.Write');
  const { data, isLoading, isError } = useDepartments();
  const createDepartment = useCreateDepartment();
  const { register, handleSubmit, reset } = useForm<DeptForm>();
  const [formError, setFormError] = useState<string | null>(null);
  const [query, setQuery] = useState('');
  const [showForm, setShowForm] = useState(true);
  const [editing, setEditing] = useState<Department | null>(null);
  const [confirming, setConfirming] = useState<Department | null>(null);
  const toast = useToast();

  const q = query.trim().toLowerCase();
  const filtered = (data ?? []).filter(
    (d) => !q || d.code.toLowerCase().includes(q) || d.name.toLowerCase().includes(q),
  );

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await createDepartment.mutateAsync(values);
      reset();
      toast('Department added.');
    } catch (error) {
      setFormError(messageFor(error));
    }
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="Departments"
        subtitle="Organise your workforce into departments."
        actions={
          canWrite ? (
            <Button onClick={() => setShowForm((v) => !v)}>
              {showForm ? 'Hide form' : 'New department'}
            </Button>
          ) : undefined
        }
      />

      {canWrite && showForm && (
        <Card>
          <form onSubmit={onSubmit} className="flex flex-wrap items-end gap-3">
            <Field id="code" label="Code" required className="w-32">
              <Input id="code" {...register('code', { required: true })} />
            </Field>
            <Field id="name" label="Name" required className="min-w-52 flex-1">
              <Input id="name" {...register('name', { required: true })} />
            </Field>
            <Button type="submit" variant="primary" loading={createDepartment.isPending}>
              {createDepartment.isPending ? 'Adding…' : 'Add department'}
            </Button>
            {formError && <span role="alert" className="text-sm font-medium text-[var(--color-danger)]">{formError}</span>}
          </form>
        </Card>
      )}

      <AsyncView isLoading={isLoading} isError={isError} isEmpty={data?.length === 0} emptyText="No departments yet.">
        {data && (
          <div className="space-y-4">
            <Toolbar>
              <SearchInput
                value={query}
                onChange={setQuery}
                label="Search departments"
                placeholder="Code or name…"
              />
            </Toolbar>

            {filtered.length === 0 && q ? (
              <EmptyState title="No departments match your search." />
            ) : (
              <DataTable
                head={
                  <tr>
                    <Th module="departments">Code</Th>
                    <Th module="departments">Name</Th>
                    <Th module="departments">Status</Th>
                    {canWrite && <Th module="departments"><span className="sr-only">Actions</span></Th>}
                  </tr>
                }
              >
                {filtered.map((d) => (
                  <Tr key={d.id}>
                    <Td className="font-mono font-medium text-[var(--color-ink)]">{d.code}</Td>
                    <Td>{d.name}</Td>
                    <Td>
                      <StatusPill tone={d.isActive ? 'success' : 'neutral'} label={d.isActive ? 'Active' : 'Inactive'} />
                      <span className="sr-only">{d.isActive ? '● Active' : '○ Inactive'}</span>
                    </Td>
                    {canWrite && (
                      <Td>
                        <RowActions
                          actions={[
                            { label: 'Edit', onClick: () => setEditing(d) },
                            d.isActive
                              ? { label: 'Deactivate', tone: 'danger', onClick: () => setConfirming(d) }
                              : { label: 'Activate', onClick: () => setConfirming(d) },
                          ]}
                        />
                      </Td>
                    )}
                  </Tr>
                ))}
              </DataTable>
            )}
          </div>
        )}
      </AsyncView>

      {editing && (
        <EditDepartmentModal
          department={editing}
          onClose={() => setEditing(null)}
        />
      )}

      {confirming && (
        <ToggleActiveDialog
          department={confirming}
          onClose={() => setConfirming(null)}
        />
      )}
    </div>
  );
}

interface EditForm {
  name: string;
}

function EditDepartmentModal({ department, onClose }: { department: Department; onClose: () => void }) {
  const updateDepartment = useUpdateDepartment();
  const toast = useToast();
  const [serverError, setServerError] = useState<string | null>(null);
  const { register, handleSubmit } = useForm<EditForm>({ defaultValues: { name: department.name } });

  const onSubmit = handleSubmit(async (values) => {
    setServerError(null);
    try {
      await updateDepartment.mutateAsync({
        id: department.id,
        name: values.name,
        parentDepartmentId: department.parentDepartmentId ?? null,
      });
      toast('Department updated.');
      onClose();
    } catch (error) {
      setServerError(messageFor(error));
    }
  });

  return (
    <Modal
      title={`Edit ${department.code}`}
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose}>Cancel</Button>
          <Button variant="primary" loading={updateDepartment.isPending} onClick={onSubmit}>Save</Button>
        </>
      }
    >
      <form onSubmit={onSubmit} className="space-y-3">
        <Field id="edit-name" label="Name" required>
          <Input id="edit-name" {...register('name', { required: true })} />
        </Field>
        {serverError && <p role="alert" className="text-sm font-medium text-[var(--color-danger)]">{serverError}</p>}
      </form>
    </Modal>
  );
}

function ToggleActiveDialog({ department, onClose }: { department: Department; onClose: () => void }) {
  const setActive = useSetDepartmentActive();
  const toast = useToast();
  const activating = !department.isActive;

  const onConfirm = async () => {
    try {
      await setActive.mutateAsync({ id: department.id, active: activating });
      toast(activating ? 'Department activated.' : 'Department deactivated.');
      onClose();
    } catch (error) {
      toast(messageFor(error), 'error');
      onClose();
    }
  };

  return (
    <ConfirmDialog
      title={activating ? `Activate ${department.code}?` : `Deactivate ${department.code}?`}
      message={
        activating
          ? `Reactivate ${department.name}. It will become available for assignment again.`
          : `Deactivate ${department.name}. It will no longer be available for new assignments.`
      }
      confirmLabel={activating ? 'Activate' : 'Deactivate'}
      tone={activating ? 'primary' : 'danger'}
      loading={setActive.isPending}
      onConfirm={onConfirm}
      onCancel={onClose}
    />
  );
}
