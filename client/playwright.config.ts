import { defineConfig, devices } from '@playwright/test';

// E2E runs the real SPA against the real API against a dedicated LocalDB database
// (TAMS_E2E) so it never touches dev/integration data. The webServer block boots
// both: the API (with the E2E connection string + relaxed auth rate limit for the
// single test IP) and the Vite dev server (which proxies /api to the API).
const API_URL = 'http://localhost:5099';
const SPA_URL = 'http://localhost:5174';

const E2E_CONNECTION =
  'Server=(localdb)\\MSSQLLocalDB;Database=TAMS_E2E;Trusted_Connection=True;TrustServerCertificate=true;MultipleActiveResultSets=true';

export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,     // shared DB → run serially
  workers: 1,
  retries: 0,
  reporter: [['list']],
  use: {
    baseURL: SPA_URL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      // API — recreates the E2E DB fresh via EnsureDeleted+Migrate at startup is
      // not automatic, so we let the app migrate+seed on boot (Development).
      command:
        `dotnet run --project ../src/TAMS.Api --no-launch-profile ` +
        `--urls ${API_URL}`,
      url: `${API_URL}/api/v1/health/ready`,
      timeout: 120_000,
      reuseExistingServer: false,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        ConnectionStrings__Default: E2E_CONNECTION,
        Jwt__SigningKey: 'e2e-only-signing-key-at-least-32-bytes-long-000000',
        Seed__Enabled: 'true',
        RateLimit__AuthPerMinute: '100000',
        RateLimit__GlobalPerMinute: '100000',
      },
    },
    {
      // SPA — Vite dev server on a dedicated port, proxying /api to the E2E API.
      command: 'npm run dev -- --port 5174',
      url: SPA_URL,
      timeout: 60_000,
      reuseExistingServer: false,
      env: { VITE_E2E_API: API_URL },
    },
  ],
});
