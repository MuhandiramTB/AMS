import { describe, it, expect } from 'vitest';
import { AxiosError, AxiosHeaders } from 'axios';
import { ApiError, toApiError } from './client';
import type { ProblemDetails } from './types';

/** Builds a minimal AxiosError carrying an RFC 9457 problem-details body. */
function axiosErrorWith(status: number, problem: ProblemDetails): AxiosError<ProblemDetails> {
  const err = new AxiosError<ProblemDetails>('Request failed');
  err.response = {
    status,
    data: problem,
    statusText: '',
    headers: {},
    config: { headers: new AxiosHeaders() },
  };
  return err;
}

describe('toApiError', () => {
  it('maps RFC 9457 problem details to ApiError with status, detail, correlationId and field errors', () => {
    const err = axiosErrorWith(400, {
      title: 'Validation failed',
      detail: 'One or more fields are invalid.',
      status: 400,
      correlationId: 'abc-123',
      errors: { firstName: ['First name is required.'] },
    });

    const result = toApiError(err);

    expect(result).toBeInstanceOf(ApiError);
    expect(result.status).toBe(400);
    expect(result.message).toBe('One or more fields are invalid.');
    expect(result.correlationId).toBe('abc-123');
    expect(result.fieldErrors).toEqual({ firstName: ['First name is required.'] });
  });

  it('falls back to title when detail is absent', () => {
    const result = toApiError(axiosErrorWith(409, { title: 'Resource conflict.' }));
    expect(result.status).toBe(409);
    expect(result.message).toBe('Resource conflict.');
  });

  it('handles a non-Axios error safely', () => {
    const result = toApiError(new Error('boom'));
    expect(result).toBeInstanceOf(ApiError);
    expect(result.status).toBe(0);
    expect(result.message).toBe('An unexpected error occurred.');
  });
});
