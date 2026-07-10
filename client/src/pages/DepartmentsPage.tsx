import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useCreateDepartment, useDepartments } from '../api/hooks';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../api/client';
import { AsyncView, Button, Card, Field, Input, PageHeader, TableWrap, Th, Td, useToast } from '../components/ui';

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
  const toast = useToast();

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
      <PageHeader title="Departments" subtitle="Organise your workforce into departments." />

      {canWrite && (
        <Card>
          <form onSubmit={onSubmit} className="flex flex-wrap items-end gap-3">
            <Field id="code" label="Code" className="w-32">
              <Input id="code" {...register('code', { required: true })} />
            </Field>
            <Field id="name" label="Name" className="min-w-52 flex-1">
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
          <TableWrap>
            <thead>
              <tr>
                <Th>Code</Th>
                <Th>Name</Th>
                <Th>Status</Th>
              </tr>
            </thead>
            <tbody>
              {data.map((d) => (
                <tr key={d.id} className="border-t border-[var(--color-line-soft)] transition-colors hover:bg-[var(--color-surface-2)]">
                  <Td className="font-mono font-medium text-[var(--color-ink)]">{d.code}</Td>
                  <Td>{d.name}</Td>
                  <Td>
                    <span className={d.isActive ? 'font-medium text-[var(--color-present)]' : 'text-[var(--color-muted-soft)]'}>
                      {d.isActive ? '● Active' : '○ Inactive'}
                    </span>
                  </Td>
                </tr>
              ))}
            </tbody>
          </TableWrap>
        )}
      </AsyncView>
    </div>
  );
}
