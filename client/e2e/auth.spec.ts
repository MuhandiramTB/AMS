import { test, expect } from '@playwright/test';
import { loginAsAdmin } from './helpers';

test.describe('Authentication & navigation', () => {
  test('shows the login screen when unauthenticated', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('button', { name: /sign in/i })).toBeVisible();
  });

  test('rejects wrong credentials with a generic message', async ({ page }) => {
    await page.goto('/');
    await page.getByLabel('Username').fill('admin');
    await page.getByLabel('Password').fill('wrong-password');
    await page.getByRole('button', { name: /sign in/i }).click();
    await expect(page.getByRole('alert')).toContainText(/invalid/i);
    // Still on the login screen.
    await expect(page.getByRole('button', { name: /sign in/i })).toBeVisible();
  });

  test('admin logs in and sees the full role-filtered nav', async ({ page }) => {
    await loginAsAdmin(page);
    for (const label of ['Dashboard', 'Attendance', 'Shifts', 'Employees', 'Departments', 'Devices']) {
      await expect(page.getByRole('link', { name: label })).toBeVisible();
    }
  });

  test('logout returns to the login screen', async ({ page }) => {
    await loginAsAdmin(page);
    await page.getByRole('button', { name: /log out/i }).click();
    await expect(page.getByRole('button', { name: /sign in/i })).toBeVisible();
  });
});
