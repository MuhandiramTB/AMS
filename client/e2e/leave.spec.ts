import { test, expect, APIRequestContext } from '@playwright/test';
import { loginAsAdmin, unique } from './helpers';

const API = 'http://localhost:5099';

async function adminToken(request: APIRequestContext): Promise<string> {
  const resp = await request.post(`${API}/api/v1/auth/login`, {
    data: { userName: 'admin', password: 'ChangeMe!123' },
  });
  expect(resp.ok()).toBeTruthy();
  return (await resp.json()).accessToken as string;
}

/** Seeds a department, employee, leave type and balance; returns their ids. */
async function seedLeaveFixtures(request: APIRequestContext) {
  const token = await adminToken(request);
  const auth = { Authorization: `Bearer ${token}` };
  const s = unique('LV');

  const dept = await (await request.post(`${API}/api/v1/departments`, {
    headers: auth, data: { code: `LD${s}`, name: `LDept${s}` },
  })).json();
  const emp = await (await request.post(`${API}/api/v1/employees`, {
    headers: auth, data: { employeeNo: `LE${s}`, firstName: 'Grace', lastName: 'Hopper', primaryDepartmentId: dept.id },
  })).json();
  const type = await (await request.post(`${API}/api/v1/leave/types`, {
    headers: auth, data: { code: `T${s}`.substring(0, 12), name: `Annual ${s}` },
  })).json();
  await request.put(`${API}/api/v1/leave/balances`, {
    headers: auth, data: { employeeId: emp.id, leaveTypeId: type.id, year: 2026, entitledDays: 20 },
  });
  return { empId: emp.id, typeName: type.name };
}

test.describe('Leave journey (real browser, real API)', () => {
  test('request → approve → cancel through the UI, with status changes', async ({ page, request }) => {
    const { empId, typeName } = await seedLeaveFixtures(request);

    await loginAsAdmin(page);
    await page.getByRole('link', { name: 'Leave' }).click();

    // Submit a request through the form.
    await page.getByLabel('Employee ID', { exact: true }).fill(`${empId}`);
    await page.getByLabel('Type').selectOption({ label: typeName });
    await page.getByLabel('From').fill('2026-07-20');
    await page.getByLabel('To').fill('2026-07-22');
    await page.getByRole('button', { name: /request leave/i }).click();
    await expect(page.getByRole('status')).toContainText(/submitted/i);

    // Filter to the employee so the new request is the visible row.
    await page.getByLabel('Filter by Employee ID').fill(`${empId}`);

    // The status PILL lives in a table cell; scope there so we don't match the
    // "Leave request submitted." status-message paragraph (which is a <p>, not a cell).
    await expect(page.getByRole('cell').filter({ hasText: 'Submitted' })).toBeVisible();
    await page.getByRole('button', { name: /^approve$/i }).click();
    await expect(page.getByRole('status')).toContainText(/approved/i);
    // Status becomes Applied (approval reflects into attendance and applies).
    await expect(page.getByRole('cell').filter({ hasText: 'Applied' })).toBeVisible();

    // Cancel the applied leave; status becomes Cancelled.
    await page.getByRole('button', { name: /^cancel$/i }).click();
    await expect(page.getByRole('status')).toContainText(/cancelled/i);
    await expect(page.getByRole('cell').filter({ hasText: 'Cancelled' })).toBeVisible();
  });

  test('approving beyond the balance surfaces the over-balance error in the UI', async ({ page, request }) => {
    // Seed an employee with only a 2-day balance, then request 3 days.
    const token = await adminToken(request);
    const auth = { Authorization: `Bearer ${token}` };
    const s = unique('OB');
    const dept = await (await request.post(`${API}/api/v1/departments`, {
      headers: auth, data: { code: `OD${s}`, name: `ODept${s}` },
    })).json();
    const emp = await (await request.post(`${API}/api/v1/employees`, {
      headers: auth, data: { employeeNo: `OE${s}`, firstName: 'Over', lastName: 'Draft', primaryDepartmentId: dept.id },
    })).json();
    const type = await (await request.post(`${API}/api/v1/leave/types`, {
      headers: auth, data: { code: `TO${s}`.substring(0, 12), name: `OverType ${s}` },
    })).json();
    await request.put(`${API}/api/v1/leave/balances`, {
      headers: auth, data: { employeeId: emp.id, leaveTypeId: type.id, year: 2026, entitledDays: 2 },
    });

    await loginAsAdmin(page);
    await page.getByRole('link', { name: 'Leave' }).click();
    await page.getByLabel('Employee ID', { exact: true }).fill(`${emp.id}`);
    await page.getByLabel('Type').selectOption({ label: type.name });
    await page.getByLabel('From').fill('2026-08-10');
    await page.getByLabel('To').fill('2026-08-12'); // 3 days vs 2-day balance
    await page.getByRole('button', { name: /request leave/i }).click();

    await page.getByLabel('Filter by Employee ID').fill(`${emp.id}`);
    await page.getByRole('button', { name: /^approve$/i }).click();

    // BRULE-07: over-balance approval is blocked; the message appears in the UI.
    await expect(page.getByRole('status')).toContainText(/insufficient leave balance/i);
  });
});
