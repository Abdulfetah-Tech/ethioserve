import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PaymentApi } from './payment.api';

const EMPTY_PAGE = { items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 };

describe('PaymentApi', () => {
  let api: PaymentApi;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(PaymentApi);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('starts a payment with an idempotency key and no client-controlled amount', () => {
    api.start('request/1', 'customer-key-123').subscribe();

    const request = httpMock.expectOne('/api/customer/payments/requests/request%2F1');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeNull();
    expect(request.request.headers.get('Idempotency-Key')).toBe('customer-key-123');
    request.flush({});
  });

  it('lists customer payments with only the specified filters', () => {
    api.customerList({ requestId: 'request-1', page: 2 }, 8).subscribe();

    const request = httpMock.expectOne((r) => r.url === '/api/customer/payments');
    expect(request.request.params.get('requestId')).toBe('request-1');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('8');
    request.flush(EMPTY_PAGE);
  });

  it('sends mock-gateway outcomes using the payment action endpoint', () => {
    api.simulate('payment-1', 'Succeeded').subscribe();

    const request = httpMock.expectOne('/api/customer/payments/payment-1/simulate');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ outcome: 'Succeeded', reason: null });
    request.flush({});
  });

  it('loads provider earnings and payment history from provider-only routes', () => {
    api.providerEarnings().subscribe();
    httpMock.expectOne('/api/provider/payments/earnings').flush({});

    api.providerList(1).subscribe();
    const request = httpMock.expectOne((r) => r.url === '/api/provider/payments');
    expect(request.request.params.get('page')).toBeNull();
    expect(request.request.params.get('pageSize')).toBe('10');
    request.flush(EMPTY_PAGE);
  });
});
