import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

// Unmount React trees between tests to keep them isolated (07 §10 test conventions).
afterEach(() => cleanup());
