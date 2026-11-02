import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ProviderPayments } from './provider-payments';

describe('ProviderPayments', () => {
  let httpMock: HttpTestingController;
  let fixture: ComponentFixture<ProviderPayments>;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ProviderPayments);
    await fixture.whenStable();
  });

  afterEach(() => httpMock.verify());

  it('loads earnings and payment history and renders the privacy-safe summary', async () => {
    httpMock.expectOne('/api/provider/payments/earnings').flush({
      totalReceived: 500,
      paymentsReceived: 1,
      awaitingPaymentCount: 1,
      awaitingPaymentAmount: 250,
      currency: 'ETB',
    });
    httpMock.expectOne((request) => request.url === '/api/provider/payments').flush({
      items: [{
        id: 'payment-1',
        requestId: 'request-1',
        requestTitle: 'Repair the sink',
        customerName: 'Test User',
        amount: 500,
        currency: 'ETB',
        status: 'Completed',
        reference: 'ES-20261102-ABCDEFGH2345',
        paidAt: '2026-11-02T10:05:00Z',
        createdAt: '2026-11-02T10:00:00Z',
      }],
      page: 1,
      pageSize: 10,
      totalCount: 1,
      totalPages: 1,
    });
    await fixture.whenStable();

    const content = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(content).toContain('Earnings summary');
    expect(content).toContain('Payments received');
    expect(content).toContain('Test User');
    expect(content).toContain('Repair the sink');
    expect(content).toContain('ES-20261102-ABCDEFGH2345');
    expect(content).not.toContain('customerPhone');
  });

  it('shows an explicit loading error when either provider payment call fails', async () => {
    const calls = httpMock.match((request) =>
      request.url === '/api/provider/payments/earnings' || request.url === '/api/provider/payments',
    );
    expect(calls).toHaveLength(2);
    calls.find((request) => request.request.url.endsWith('/earnings'))?.flush({
      totalReceived: 0,
      paymentsReceived: 0,
      awaitingPaymentCount: 0,
      awaitingPaymentAmount: 0,
      currency: 'ETB',
    });
    calls.find((request) => request.request.url === '/api/provider/payments')?.flush(
      { title: 'Unavailable', detail: 'Earnings are temporarily unavailable.' },
      { status: 503, statusText: 'Unavailable' },
    );
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Earnings are temporarily unavailable.');
  });
});
