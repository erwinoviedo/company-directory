import { Component, input, output } from '@angular/core';
import { Company } from '../company.models';

/** Shows companies in a table. Purely presentational: it raises events and owns no data. */
@Component({
  selector: 'app-company-list',
  templateUrl: './company-list.html',
})
export class CompanyList {
  readonly companies = input.required<Company[]>();
  readonly totalCount = input(0);
  readonly loading = input(false);
  /** The active search text, used to word the empty state and to show the match column. */
  readonly searchText = input('');

  readonly edit = output<Company>();
  readonly remove = output<Company>();
}
