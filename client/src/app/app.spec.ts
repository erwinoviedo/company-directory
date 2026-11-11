import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { App } from './app';
import { Company, Page } from './companies/company.models';

describe('App', () => {
  let fixture: ComponentFixture<App>;
  let http: HttpTestingController;
  let element: HTMLElement;

  const page = (items: Company[]): Page<Company> => ({ items, page: 1, pageSize: 50, totalCount: items.length });

  const acme: Company = {
    id: '11111111-1111-1111-1111-111111111111',
    name: 'Acme',
    websiteUrl: 'https://www.acme.com/',
    domain: 'acme.com',
    createdAt: '2026-01-15T09:30:00Z',
    updatedAt: null,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    fixture = TestBed.createComponent(App);
    http = TestBed.inject(HttpTestingController);
    element = fixture.nativeElement;
  });

  afterEach(() => http.verify());

  it('loads the stored companies from the API and lists them', async () => {
    http.expectOne({ method: 'GET', url: '/api/companies' }).flush(page([acme]));
    await fixture.whenStable();

    const cells = [...element.querySelectorAll('tbody td')].map((cell) => cell.textContent?.trim());
    expect(cells.slice(0, 3)).toEqual(['Acme', 'https://www.acme.com/', 'acme.com']);
  });

  it('invites the user to add a company when there are none', async () => {
    http.expectOne('/api/companies').flush(page([]));
    await fixture.whenStable();

    expect(element.textContent).toContain('No companies yet');
    expect(element.querySelector('table')).toBeNull();
  });

  it('says so when the API cannot be reached', async () => {
    http.expectOne('/api/companies').error(new ProgressEvent('error'), { status: 0 });
    await fixture.whenStable();

    expect(element.querySelector('.notice.error')?.textContent).toContain('Cannot reach the server');
  });

  it('reloads the list and confirms after a company is added', async () => {
    http.expectOne('/api/companies').flush(page([]));
    await fixture.whenStable();

    const type = (id: string, value: string) => {
      const input = element.querySelector<HTMLInputElement>(`#${id}`)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    type('name', 'Acme');
    type('websiteUrl', 'https://www.acme.com');
    element.querySelector('form')!.dispatchEvent(new Event('submit'));

    http.expectOne({ method: 'POST', url: '/api/companies' }).flush(acme, { status: 201, statusText: 'Created' });
    http.expectOne({ method: 'GET', url: '/api/companies' }).flush(page([acme]));
    await fixture.whenStable();

    expect(element.querySelector('.notice.success')?.textContent).toContain('Acme was added.');
    expect(element.querySelector('tbody tr')?.textContent).toContain('Acme');
  });
});
