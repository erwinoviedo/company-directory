import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Company } from '../company.models';
import { CompanyForm, CompanySaved } from './company-form';

describe('CompanyForm', () => {
  let fixture: ComponentFixture<CompanyForm>;
  let http: HttpTestingController;
  let element: HTMLElement;
  let saved: CompanySaved[];

  const acme: Company = {
    id: '11111111-1111-1111-1111-111111111111',
    name: 'Acme',
    websiteUrl: 'https://www.acme.com/',
    domain: 'acme.com',
    createdAt: '2026-01-15T09:30:00Z',
    updatedAt: null,
  };

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [CompanyForm],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    fixture = TestBed.createComponent(CompanyForm);
    http = TestBed.inject(HttpTestingController);
    element = fixture.nativeElement;
    saved = [];
    fixture.componentInstance.saved.subscribe((event) => saved.push(event));
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  function type(id: 'name' | 'websiteUrl', value: string): void {
    const input = element.querySelector<HTMLInputElement>(`#${id}`)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  async function submit(): Promise<void> {
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  }

  function errorFor(id: 'name' | 'websiteUrl'): string | undefined {
    return element.querySelector(`#${id}-error`)?.textContent?.trim();
  }

  it('shows what is missing and sends nothing when submitted empty', async () => {
    await submit();

    expect(errorFor('name')).toBe('Company name is required.');
    expect(errorFor('websiteUrl')).toBe('Website URL is required.');
    http.expectNone('/api/companies');
  });

  it('rejects a short name and a malformed address before calling the API', async () => {
    type('name', ' A ');
    type('websiteUrl', 'acme');
    await submit();

    expect(errorFor('name')).toBe('Company name must be at least 3 characters.');
    expect(errorFor('websiteUrl')).toBe('Enter a full web address, for example https://www.example.com.');
    http.expectNone('/api/companies');
  });

  it('posts trimmed values, announces the new company and clears the form', async () => {
    type('name', '  Acme ');
    type('websiteUrl', ' https://www.acme.com ');
    await submit();

    const request = http.expectOne({ method: 'POST', url: '/api/companies' });
    expect(request.request.body).toEqual({ name: 'Acme', websiteUrl: 'https://www.acme.com' });
    request.flush(acme, { status: 201, statusText: 'Created' });
    await fixture.whenStable();

    expect(saved).toEqual([{ company: acme, created: true }]);
    expect(element.querySelector<HTMLInputElement>('#name')!.value).toBe('');
    expect(errorFor('name')).toBeUndefined();
  });

  it('shows the message the API returns for a field, until the value changes', async () => {
    type('name', 'Globex');
    type('websiteUrl', 'https://www.acme.com');
    await submit();

    const message = "Company name 'Globex' does not appear to match the website 'acme.com'.";
    http
      .expectOne('/api/companies')
      .flush({ detail: message, errors: { name: [message] } }, { status: 400, statusText: 'Bad Request' });
    await fixture.whenStable();

    expect(errorFor('name')).toBe(message);
    expect(saved).toEqual([]);

    type('name', 'Acme');
    await fixture.whenStable();
    expect(errorFor('name')).toBeUndefined();
  });

  it('shows an API error that is not about a field, such as a duplicate', async () => {
    type('name', 'Acme');
    type('websiteUrl', 'https://www.acme.com');
    await submit();

    http
      .expectOne('/api/companies')
      .flush(
        { detail: "A company with the website 'acme.com' already exists." },
        { status: 409, statusText: 'Conflict' },
      );
    await fixture.whenStable();

    expect(element.querySelector('[role="alert"]')?.textContent).toContain('already exists');
  });

  it('loads a company to edit and saves it with PUT', async () => {
    fixture.componentRef.setInput('company', acme);
    await fixture.whenStable();

    expect(element.querySelector('h2')?.textContent).toContain('Edit company');
    expect(element.querySelector<HTMLInputElement>('#name')!.value).toBe('Acme');

    type('name', 'Acme Inc');
    await submit();

    const request = http.expectOne({ method: 'PUT', url: `/api/companies/${acme.id}` });
    expect(request.request.body).toEqual({ name: 'Acme Inc', websiteUrl: 'https://www.acme.com/' });
    const updated = { ...acme, name: 'Acme Inc' };
    request.flush(updated);
    await fixture.whenStable();

    expect(saved).toEqual([{ company: updated, created: false }]);
  });
});
