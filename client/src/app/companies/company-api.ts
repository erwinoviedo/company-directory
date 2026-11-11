import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Company, CompanyInput, CompanySearch, Page } from './company.models';

/** Typed client for the companies REST API. The dev server proxies /api to the backend. */
@Injectable({ providedIn: 'root' })
export class CompanyApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/companies';

  search(search: CompanySearch = {}): Observable<Page<Company>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(search)) {
      if (value !== undefined && value !== '') {
        params = params.set(key, value);
      }
    }

    return this.http.get<Page<Company>>(this.url, { params });
  }

  get(id: string): Observable<Company> {
    return this.http.get<Company>(`${this.url}/${id}`);
  }

  create(input: CompanyInput): Observable<Company> {
    return this.http.post<Company>(this.url, input);
  }

  update(id: string, input: CompanyInput): Observable<Company> {
    return this.http.put<Company>(`${this.url}/${id}`, input);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
