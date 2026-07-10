import { test, expect, APIRequestContext } from '@playwright/test';
import { loginAsAdmin, unique } from './helpers';

const API = 'http://localhost:5099';

/** Gets an admin bearer token directly from the API (for test-setup calls). */
async function adminToken(request: APIRequestContext): Promise<string> {
  const resp = await request.post(`${API}/api/v1/auth/login`, {
    data: { userName: 'admin', password: 'ChangeMe!123' },
  });
  expect(resp.ok()).toBeTruthy();
  return (await resp.json()).accessToken as string;
}

test.describe('Core workforce journey (real browser, real API)', () => {
  test('create department and employee through the UI', async ({ page }) => {
    await loginAsAdmin(page);
    const code = unique('D');

    // Department
    await page.getByRole('link', { name: 'Departments' }).click();
    await page.getByLabel('Code').fill(code);
    await page.getByLabel('Name').fill('E2E Dept');
    await page.getByRole('button', { name: /add department/i }).click();
    await expect(page.getByText(code)).toBeVisible();

    // Employee (in that department — pick it from the select by visible name)
    const empNo = unique('E');
    await page.getByRole('link', { name: 'Employees' }).click();
    await page.locator('#employeeNo').fill(empNo);
    await page.locator('#firstName').fill('Ada');
    await page.locator('#lastName').fill('Lovelace');
    await page.locator('#primaryDepartmentId').selectOption({ label: 'E2E Dept' });
    await page.getByRole('button', { name: /add employee/i }).click();
    await expect(page.getByText(empNo)).toBeVisible();
  });

  test('create a shift and register a device through the UI', async ({ page }) => {
    await loginAsAdmin(page);

    // Shift
    const shiftCode = unique('S');
    await page.getByRole('link', { name: 'Shifts' }).click();
    await page.getByPlaceholder('Code').fill(shiftCode);
    await page.getByPlaceholder('Name').fill('E2E Day');
    await page.locator('input[type="time"]').first().fill('09:00');
    await page.locator('input[type="time"]').nth(1).fill('17:00');
    await page.getByRole('button', { name: /create shift/i }).click();
    await expect(page.getByText(shiftCode)).toBeVisible();

    // Device
    const serial = unique('ZK');
    await page.getByRole('link', { name: 'Devices' }).click();
    await page.getByPlaceholder('Serial no').fill(serial);
    await page.getByPlaceholder('Name').fill('E2E Gate');
    await page.getByRole('button', { name: /register device/i }).click();
    await expect(page.getByText(serial)).toBeVisible();
  });

  test('correct an attendance record with a mandatory reason (the review spine)', async ({ page, request }) => {
    // --- Setup via API: a dept, employee, shift+assignment, punches, processed record ---
    const token = await adminToken(request);
    const auth = { Authorization: `Bearer ${token}` };
    const suffix = unique('J');

    const dept = await (await request.post(`${API}/api/v1/departments`, {
      headers: auth, data: { code: `JD${suffix}`, name: `JDept${suffix}` },
    })).json();

    const emp = await (await request.post(`${API}/api/v1/employees`, {
      headers: auth,
      data: { employeeNo: `JE${suffix}`, firstName: 'Joan', lastName: 'Clarke', primaryDepartmentId: dept.id },
    })).json();

    const shift = await (await request.post(`${API}/api/v1/shifts`, {
      headers: auth,
      data: { code: `JS${suffix}`, name: 'JDay', startTime: '09:00:00', endTime: '17:00:00',
              breakMinutes: 60, graceInMinutes: 10, graceOutMinutes: 10, overtimeThresholdMinutes: 0 },
    })).json();
    await request.post(`${API}/api/v1/shifts/assignments`, {
      headers: auth, data: { shiftId: shift.id, employeeId: emp.id, effectiveFrom: '2026-07-01' },
    });

    // Register a device for the punches (don't assume device 1 exists).
    const device = await (await request.post(`${API}/api/v1/devices`, {
      headers: auth, data: { serialNo: `JZK${suffix}`, name: `JGate${suffix}`, ipAddress: '127.0.0.1', port: 4370, model: 'K40' },
    })).json();

    // Late arrival so the record is meaningful.
    await request.post(`${API}/api/v1/attendance/punches`, {
      headers: auth, data: { deviceId: device.id, deviceUserId: `${emp.id}`, employeeId: emp.id, punchedAtUtc: '2026-07-10T09:25:00Z', direction: 1 },
    });
    await request.post(`${API}/api/v1/attendance/punches`, {
      headers: auth, data: { deviceId: device.id, deviceUserId: `${emp.id}`, employeeId: emp.id, punchedAtUtc: '2026-07-10T17:00:00Z', direction: 2 },
    });
    await request.post(`${API}/api/v1/attendance/process`, {
      headers: auth, data: { employeeId: emp.id, workDate: '2026-07-10' },
    });

    // --- Now drive the correction through the UI ---
    await loginAsAdmin(page);
    await page.getByRole('link', { name: 'Attendance' }).click();
    await page.getByLabel('Employee ID').fill(`${emp.id}`);

    // Open the record's correction drawer.
    await page.getByRole('button', { name: 'Review' }).first().click();
    await expect(page.getByRole('heading', { name: /Record —/ })).toBeVisible();

    // Attempt save WITHOUT a reason → blocked (BRULE-05).
    await page.getByRole('button', { name: /save & recalculate/i }).click();
    await expect(page.getByText(/a reason is required/i)).toBeVisible();

    // Provide a reason and save → drawer closes (success).
    await page.getByLabel(/reason/i).fill('CCTV confirms 09:00 arrival; badge failed.');
    await page.getByRole('button', { name: /save & recalculate/i }).click();
    await expect(page.getByRole('heading', { name: /Record —/ })).toBeHidden();
  });
});
