import js from '@eslint/js';
import globals from 'globals';
import reactHooks from 'eslint-plugin-react-hooks';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  { ignores: ['node_modules', 'playwright-report', 'test-results'] },
  {
    extends: [js.configs.recommended, ...tseslint.configs.recommended],
    files: ['**/*.{ts,tsx}'],
    languageOptions: { ecmaVersion: 2023, globals: { ...globals.browser, ...globals.node } },
    plugins: { 'react-hooks': reactHooks },
    rules: {
      ...reactHooks.configs.recommended.rules,
      // NFR-017 / Q-232: no script-readable token persistence anywhere in the SPA.
      'no-restricted-globals': ['error',
        { name: 'localStorage', message: 'Tokens must never be persisted to script-readable storage (NFR-017).' },
        { name: 'sessionStorage', message: 'Tokens must never be persisted to script-readable storage (NFR-017).' }],
    },
  },
  // tests may read storage to PROVE nothing was written there
  { files: ['e2e/**', '**/*.test.{ts,tsx}'], rules: { 'no-restricted-globals': 'off' } },
);
