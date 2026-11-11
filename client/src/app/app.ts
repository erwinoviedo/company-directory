import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap, tap } from 'rxjs';
import { toApiError } from './companies/api-error';
import { CompanyApi } from './companies/company-api';
import { CompanyForm, CompanySaved } from './companies/company-form/company-form';
import { CompanyList } from './companies/company-list/company-list';
import { Company } from './companies/company.models';

interface Notice {
  kind: 'success' | 'error';
  text: string;
}

/** The single page: a form to add or edit a company, and the searchable list of stored companies. */
@Component({
  selector: 'app-root',
  imports: [CompanyForm, CompanyList],
  templateUrl: './app.html',
})
export class App {
  private readonly api = inject(CompanyApi);

  protected readonly companies = signal<Company[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(false);
  protected readonly searchText = signal('');
  protected readonly editing = signal<Company | null>(null);
  protected readonly notice = signal<Notice | null>(null);

  private readonly reload = new Subject<void>();
  private readonly searchInput = new Subject<string>();

  constructor() {
    // Every (re)load goes through one stream so a slow, outdated response can never
    // overwrite a newer one: switchMap drops the request that is still in flight.
    this.reload
      .pipe(
        tap(() => this.loading.set(true)),
        switchMap(() =>
          this.api.search({ q: this.searchText() }).pipe(
            catchError((error: unknown) => {
              this.notice.set({ kind: 'error', text: toApiError(error).message });
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((page) => {
        this.loading.set(false);
        if (page) {
          this.companies.set(page.items);
          this.totalCount.set(page.totalCount);
        }
      });

    this.searchInput
      .pipe(debounceTime(250), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((text) => {
        this.searchText.set(text);
        this.reload.next();
      });

    this.reload.next();
  }

  protected onSearch(event: Event): void {
    this.searchInput.next((event.target as HTMLInputElement).value.trim());
  }

  protected onSaved({ company, created }: CompanySaved): void {
    this.editing.set(null);
    this.notice.set({ kind: 'success', text: `${company.name} was ${created ? 'added' : 'updated'}.` });
    this.reload.next();
  }

  protected startEdit(company: Company): void {
    this.notice.set(null);
    this.editing.set(company);
  }

  protected remove(company: Company): void {
    if (!confirm(`Delete ${company.name}? This cannot be undone.`)) {
      return;
    }

    this.api.delete(company.id).subscribe({
      next: () => {
        if (this.editing()?.id === company.id) {
          this.editing.set(null);
        }
        this.notice.set({ kind: 'success', text: `${company.name} was deleted.` });
        this.reload.next();
      },
      error: (error: unknown) => {
        this.notice.set({ kind: 'error', text: toApiError(error).message });
        this.reload.next(); // it may already be gone; show the current truth
      },
    });
  }
}
