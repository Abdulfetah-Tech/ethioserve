import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { CustomerQuote } from '../../core/models/quote.models';
import { RequestStatus } from '../../core/models/request.models';
import { RequestQuotes } from './request-quotes';

const inDays = (days: number): string => new Date(Date.now() + days * 86_400_000).toISOString();

function quote(id: string, name: string, amount: number, overrides: Partial<CustomerQuote> = {}): CustomerQuote {
  return {
    id,
    providerId: `provider-${id}`,
    providerName: name,
    providerRating: 4.5,
    providerReviews: 10,
    providerYearsOfExperience: 6,
    amount,
    description: 'Parts and labour included.',
    estimatedDurationMinutes: 120,
    validUntil: inDays(3),
    status: 'Pending',
    createdAt: new Date().toISOString(),
    providerPhone: null,
    ...overrides,
  };
}

describe('RequestQuotes', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  async function render(quotes: CustomerQuote[], status: RequestStatus = 'Quoted'): Promise<ComponentFixture<RequestQuotes>> {
    const fixture = TestBed.createComponent(RequestQuotes);
    fixture.componentRef.setInput('requestId', 'r1');
    fixture.componentRef.setInput('requestStatus', status);
    await fixture.whenStable();

    httpMock.expectOne('/api/requests/r1/quotes').flush(quotes);
    await fixture.whenStable();

    return fixture;
  }

  const element = (fixture: ComponentFixture<RequestQuotes>): HTMLElement => fixture.nativeElement as HTMLElement;

  const button = (fixture: ComponentFixture<RequestQuotes>, text: string): HTMLButtonElement | undefined =>
    Array.from(element(fixture).querySelectorAll('button')).find((b) => b.textContent?.trim() === text);

  it('lists the quotes in the order the API sends them and marks the lowest price', async () => {
    const fixture = await render([quote('q1', 'Alpha Plumbing', 300), quote('q2', 'Beta Plumbing', 450)]);

    const rows = element(fixture).querySelectorAll('tbody tr');

    expect(rows).toHaveLength(2);
    expect(rows[0].textContent).toContain('Alpha Plumbing');
    expect(rows[0].textContent).toContain('Lowest price');
    expect(rows[1].textContent).not.toContain('Lowest price');
  });

  it('does not mark a lowest price when only one quote is open', async () => {
    const fixture = await render([quote('q1', 'Alpha Plumbing', 300)]);

    expect(element(fixture).textContent).not.toContain('Lowest price');
  });

  it('explains an empty list according to the request status', async () => {
    const fixture = await render([], 'Pending');

    expect(element(fixture).textContent).toContain('No quotes yet');
  });

  it('shows no accept or decline buttons once the request is accepted, but shows the provider phone to call', async () => {
    const fixture = await render(
      [
        quote('q1', 'Alpha Plumbing', 300, { status: 'Accepted', providerPhone: '0911556677' }),
        quote('q2', 'Beta Plumbing', 450, { status: 'Rejected' }),
      ],
      'Accepted',
    );

    expect(element(fixture).querySelectorAll('button')).toHaveLength(2);
    expect(button(fixture, 'Accept')).toBeUndefined();
    expect(button(fixture, 'Decline')).toBeUndefined();
    expect(element(fixture).querySelector('a[href="tel:0911556677"]')).not.toBeNull();
  });

  it('opens the private conversation for only the selected provider quote', async () => {
    const fixture = await render([quote('q1', 'Alpha Plumbing', 300), quote('q2', 'Beta Plumbing', 450)]);

    button(fixture, 'Message provider')?.click();
    await fixture.whenStable();

    const messages = httpMock.expectOne(
      (request) =>
        request.url === '/api/requests/r1/messages' &&
        request.params.get('providerId') === 'provider-q1' &&
        request.params.get('pageSize') === '50',
    );
    messages.flush({ items: [], page: 1, pageSize: 50, totalCount: 0, totalPages: 0 });
    await fixture.whenStable();

    expect(element(fixture).textContent).toContain('Conversation with Alpha Plumbing');
    expect(element(fixture).textContent).not.toContain('Conversation with Beta Plumbing');
  });

  it('asks for confirmation before accepting, then sends the request and tells the page', async () => {
    const fixture = await render([quote('q1', 'Alpha Plumbing', 300), quote('q2', 'Beta Plumbing', 450)]);
    let changed = 0;
    fixture.componentInstance.changed.subscribe(() => changed++);

    button(fixture, 'Accept')?.click();
    await fixture.whenStable();

    expect(element(fixture).textContent).toContain('shares your name, phone number and address');
    httpMock.expectNone('/api/requests/r1/quotes/q1/accept');

    button(fixture, 'Yes, accept')?.click();

    const post = httpMock.expectOne('/api/requests/r1/quotes/q1/accept');
    expect(post.request.method).toBe('POST');
    post.flush(quote('q1', 'Alpha Plumbing', 300, { status: 'Accepted', providerPhone: '0911556677' }));
    await fixture.whenStable();

    httpMock.expectOne('/api/requests/r1/quotes').flush([
      quote('q1', 'Alpha Plumbing', 300, { status: 'Accepted', providerPhone: '0911556677' }),
      quote('q2', 'Beta Plumbing', 450, { status: 'Rejected' }),
    ]);
    await fixture.whenStable();

    expect(changed).toBe(1);
    expect(element(fixture).textContent).toContain('0911556677');
  });

  it('backs out of accepting without sending anything', async () => {
    const fixture = await render([quote('q1', 'Alpha Plumbing', 300), quote('q2', 'Beta Plumbing', 450)]);

    button(fixture, 'Accept')?.click();
    await fixture.whenStable();
    button(fixture, 'Keep looking')?.click();
    await fixture.whenStable();

    expect(button(fixture, 'Yes, accept')).toBeUndefined();
    expect(button(fixture, 'Accept')).toBeDefined();
  });
});