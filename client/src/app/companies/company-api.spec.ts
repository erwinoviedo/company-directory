import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CompanyApi } from './company-api';
import { Company, Page } from './company.models';

describe('CompanyApi', () => {
  let api: CompanyApi;
  let http: HttpTestingController;

  const acme: Company = {
    id: '11111111-1111-1111-1111-111111111111',
    name: 'Acme',
    websiteUrl: 'https://www.acme.com/',
    domain: 'acme.com',
    createdAt: '2026-01-15T09:30:00Z',
    updatedAt: null,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(CompanyApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists companies without query parameters by default', () => {
    const page: Page<Company> = { items: [acme], page: 1, pageSize: 50, totalCount: 1 };
    let received: Page<Company> | undefined;

    api.search().subscribe((result) => (received = result));

    const request = http.expectOne('/api/companies');
    expect(request.request.method).toBe('GET');
    request.flush(page);
    expect(received).toEqual(page);
  });

  it('sends only the search options that have a value', () => {
    api.search({ q: 'acme', name: '', domain: undefined, page: 2, pageSize: 10 }).subscribe();

    const request = http.expectOne((candidate) => candidate.url === '/api/companies');
    expect(request.request.params.keys().sort()).toEqual(['page', 'pageSize', 'q']);
    expect(request.request.params.get('q')).toBe('acme');
    expect(request.request.params.get('page')).toBe('2');
    request.flush({ items: [], page: 2, pageSize: 10, totalCount: 0 });
  });

  it('gets, creates, updates and deletes by the REST routes', () => {
    const input = { name: 'Acme', websiteUrl: 'https://www.acme.com' };

    api.get(acme.id).subscribe();
    api.create(input).subscribe();
    api.update(acme.id, input).subscribe();
    api.delete(acme.id).subscribe();

    http.expectOne({ method: 'GET', url: `/api/companies/${acme.id}` }).flush(acme);
    const create = http.expectOne({ method: 'POST', url: '/api/companies' });
    expect(create.request.body).toEqual(input);
    create.flush(acme);
    const update = http.expectOne({ method: 'PUT', url: `/api/companies/${acme.id}` });
    expect(update.request.body).toEqual(input);
    update.flush(acme);
    http.expectOne({ method: 'DELETE', url: `/api/companies/${acme.id}` }).flush(null);
  });
});
