import { Component, DestroyRef, effect, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { toApiError } from '../api-error';
import { CompanyApi } from '../company-api';
import { companyName, validationMessage, websiteUrl } from '../company-validators';
import { Company } from '../company.models';

export interface CompanySaved {
  company: Company;
  created: boolean;
}

type Field = 'name' | 'websiteUrl';

/** Adds a company, or edits the one passed in. Shows client and server validation messages. */
@Component({
  selector: 'app-company-form',
  imports: [ReactiveFormsModule],
  templateUrl: './company-form.html',
})
export class CompanyForm {
  private readonly api = inject(CompanyApi);
  private readonly destroyRef = inject(DestroyRef);

  /** The company being edited, or null to add a new one. */
  readonly company = input<Company | null>(null);
  readonly saved = output<CompanySaved>();
  readonly cancelled = output<void>();

  protected readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: companyName }),
    websiteUrl: new FormControl('', { nonNullable: true, validators: websiteUrl }),
  });

  protected readonly saving = signal(false);
  protected readonly submitted = signal(false);
  /** Field messages returned by the API for the last submission. */
  protected readonly serverErrors = signal<Record<string, string[]>>({});
  /** An API error that does not belong to a single field (conflict, server down). */
  protected readonly formError = signal<string | null>(null);

  constructor() {
    // Load the company to edit, or clear the form when editing ends.
    effect(() => {
      const company = this.company();
      this.form.reset({ name: company?.name ?? '', websiteUrl: company?.websiteUrl ?? '' });
      this.submitted.set(false);
    });

    // A server message describes the values that were sent; drop it once they change.
    this.form.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      this.serverErrors.set({});
      this.formError.set(null);
    });
  }

  protected fieldError(field: Field): string | null {
    const control = this.form.controls[field];
    // Stay quiet about a field the user has not typed in yet, unless they tried to submit.
    if (control.invalid && (this.submitted() || (control.touched && control.dirty))) {
      return validationMessage(field, control.errors);
    }
    return this.serverErrors()[field]?.[0] ?? null;
  }

  protected submit(): void {
    this.submitted.set(true);
    if (this.form.invalid || this.saving()) {
      return;
    }

    const editing = this.company();
    const input = {
      name: this.form.controls.name.value.trim(),
      websiteUrl: this.form.controls.websiteUrl.value.trim(),
    };
    const request = editing ? this.api.update(editing.id, input) : this.api.create(input);

    this.saving.set(true);
    request
      .pipe(
        finalize(() => this.saving.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (company) => {
          this.form.reset();
          this.submitted.set(false);
          this.saved.emit({ company, created: !editing });
        },
        error: (error: unknown) => {
          const apiError = toApiError(error);
          const hasFieldMessage = 'name' in apiError.fieldErrors || 'websiteUrl' in apiError.fieldErrors;
          this.serverErrors.set(apiError.fieldErrors);
          this.formError.set(hasFieldMessage ? null : apiError.message);
        },
      });
  }

  protected cancel(): void {
    this.cancelled.emit();
  }
}
