import { defineConfig, devices } from '@playwright/test';

// Q-234: e2e runs the SPA built in `e2e` mode (fake ACS adapter compiled in) against a mocked API —
// every /api and /hubs request is intercepted in the spec, so no backend is needed.
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  workers: 4,
  // The junit report is the machine-produced frontend test report for the phase gate, so it lands
  // beside the backend TRX in .aidlc/03-development/test-results/. Traces and other throwaway
  // artifacts go to .playwright-artifacts/ — the old name, test-results/, collided with
  // verify-gate.sh C6 ("test directory with no test files in it") and failed the gate on an
  // output directory rather than on anything real.
  reporter: [['list'], ['junit', { outputFile: '../../.aidlc/03-development/test-results/TestResults-P6-e2e-junit.xml' }]],
  outputDir: '.playwright-artifacts',
  use: { baseURL: 'http://127.0.0.1:4173', trace: 'retain-on-failure' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npx vite build --mode e2e --outDir dist-e2e --emptyOutDir && npx vite preview --outDir dist-e2e --port 4173 --strictPort --host 127.0.0.1',
    url: 'http://127.0.0.1:4173',
    reuseExistingServer: false,
    timeout: 180_000,
  },
});
