import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

// These mirror the server's rules so most mistakes are caught before a request is made.
// The server remains the authority: it alone checks that the name matches the website.

export const NAME_MIN_LENGTH = 3;
export const NAME_MAX_LENGTH = 100;

export const companyName: ValidatorFn = (control: AbstractControl<string>): ValidationErrors | null => {
  const length = control.value.trim().length;
  if (length === 0) {
    return { required: true };
  }
  if (length < NAME_MIN_LENGTH) {
    return { tooShort: true };
  }
  return length > NAME_MAX_LENGTH ? { tooLong: true } : null;
};

export const websiteUrl: ValidatorFn = (control: AbstractControl<string>): ValidationErrors | null => {
  const value = control.value.trim();
  if (value.length === 0) {
    return { required: true };
  }

  try {
    const url = new URL(value);
    const isWeb = url.protocol === 'http:' || url.protocol === 'https:';
    return isWeb && url.hostname.includes('.') ? null : { invalidUrl: true };
  } catch {
    return { invalidUrl: true };
  }
};

const messages: Record<string, Record<string, string>> = {
  name: {
    required: 'Company name is required.',
    tooShort: `Company name must be at least ${NAME_MIN_LENGTH} characters.`,
    tooLong: `Company name must be at most ${NAME_MAX_LENGTH} characters.`,
  },
  websiteUrl: {
    required: 'Website URL is required.',
    invalidUrl: 'Enter a full web address, for example https://www.example.com.',
  },
};

export function validationMessage(field: 'name' | 'websiteUrl', errors: ValidationErrors | null): string | null {
  const key = errors ? Object.keys(errors)[0] : undefined;
  return key ? (messages[field][key] ?? 'This value is not valid.') : null;
}
