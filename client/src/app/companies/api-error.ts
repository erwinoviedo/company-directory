import { HttpErrorResponse } from '@angular/common/http';

/** An API failure reduced to what the UI needs to show. */
export interface ApiError {
  status: number;
  /** One sentence suitable for display. */
  message: string;
  /** Messages keyed by form field ("name", "websiteUrl"). Empty when the error is not about a field. */
  fieldErrors: Record<string, string[]>;
}

/** The RFC 7807 body the API sends with every error. */
interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

const fallbackMessage = 'Something went wrong. Please try again.';

export function toApiError(error: unknown): ApiError {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: 0, message: fallbackMessage, fieldErrors: {} };
  }

  if (error.status === 0 || error.status >= 502) {
    return {
      status: error.status,
      message: 'Cannot reach the server. Check that the API is running and try again.',
      fieldErrors: {},
    };
  }

  const problem: ProblemDetails = typeof error.error === 'object' && error.error !== null ? error.error : {};
  return {
    status: error.status,
    message: problem.detail ?? problem.title ?? fallbackMessage,
    fieldErrors: problem.errors ?? {},
  };
}
