import { Page, expect } from '@playwright/test';

/** Logs in through the real UI as the seeded bootstrap admin. */
export async function loginAsAdmin(page: Page): Promise<void> {
  await page.goto('/');
  // The app shows the login form when unauthenticated.
  await page.getByLabel('Username').fill('admin');
  await page.getByLabel('Password').fill('ChangeMe!123');
  await page.getByRole('button', { name: /sign in/i }).click();
  // After login the app shell (with nav) appears.
  await expect(page.getByRole('link', { name: 'Dashboard' })).toBeVisible();
}

/** A short unique suffix so each test's data doesn't collide in the shared DB. */
export function unique(prefix: string): string {
  return `${prefix}${Date.now().toString().slice(-6)}${Math.floor(Math.random() * 100)}`;
}
