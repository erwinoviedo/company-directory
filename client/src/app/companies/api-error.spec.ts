import { HttpErrorResponse } from '@angular/common/http';
import { toApiError } from './api-error';

describe('toApiError', () => {
  it('reads the detail and field messages from a validation problem', () => {
    const error = toApiError(
      new HttpErrorResponse({
        status: 400,
        error: {
          title: 'The request is invalid.',
          detail: 'Company name is required.',
          errors: { name: ['Company name is required.'] },
        },
      }),
    );

    expect(error).toEqual({
      status: 400,
      message: 'Company name is required.',
      fieldErrors: { name: ['Company name is required.'] },
    });
  });

  it('falls back to the title when there is no detail', () => {
    const error = toApiError(new HttpErrorResponse({ status: 404, error: { title: 'Company not found.' } }));

    expect(error.message).toBe('Company not found.');
    expect(error.fieldErrors).toEqual({});
  });

  it.each([0, 502, 504])('explains that the server is unreachable for status %i', (status) => {
    const error = toApiError(new HttpErrorResponse({ status, error: 'proxy error' }));

    expect(error.message).toContain('Cannot reach the server');
  });

  it('uses a generic message when the body is not a problem document', () => {
    expect(toApiError(new HttpErrorResponse({ status: 500, error: '<html>' })).message).toBe(
      'Something went wrong. Please try again.',
    );
    expect(toApiError(new Error('boom')).message).toBe('Something went wrong. Please try again.');
  });
});
