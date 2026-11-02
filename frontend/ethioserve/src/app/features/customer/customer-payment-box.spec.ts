import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { CustomerPayment } from '../../core/models/payment.models';
import { CustomerPaymentBox } from './customer-payment-box';

@Component({ template: '' })
class CheckoutTarget {}

function payment(status: CustomerPayment['status']): CustomerPayment {
  return {
    id: 'payment-1',
    requestId: 'request-1',
    requestTitle: 'Repair the sink',
    providerName: 'Alpha Plumbing',
    amount: 500,
    currency: 'ETB',
    method: 'Mock',
    status,
    reference: 'ES-20261102-ABCDEFGH2345',
    failureReason: null,
    paidAt: null,
    createdAt: '2026-11-02T10:00:00Z',
    checkoutPath: status === 'Processing' ? '/pay/payment-1' : null,
  };
}

describe('CustomerPaymentBox', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'pay/:id', component: CheckoutTarget }]),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  async function render(): Promise<ComponentFixture<CustomerPaymentBox>> {
    const fixture = TestBed.createComponent(CustomerPaymentBox);
    fixture.componentRef.setInput('requestId', 'request-1');
    fixture.componentRef.setInput('requestStatus', 'Completed');
    await fixture.whenStable();
    httpMock.expectOne((request) =>
      request.url === '/api/customer/payments' && request.params.get('requestId') === 'request-1',
    ).flush({ items: [], page: 1, pageSize: 5, totalCount: 0, totalPages: 0 });
    await fixture.whenStable();
    return fixture;
  }

  const element = (fixture: ComponentFixture<CustomerPaymentBox>): HTMLElement => fixture.nativeElement as HTMLElement;

  it('starts a server-priced payment and takes the customer to checkout', async () => {
    const fixture = await render();
    const start = Array.from(element(fixture).querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Start payment'));

    start?.click();
    const request = httpMock.expectOne('/api/customer/payments/requests/request-1');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeNull();
    expect(request.request.headers.has('Idempotency-Key')).toBe(true);
    request.flush(payment('Processing'));
    await fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/pay/payment-1');
  });

  it('links an open payment to checkout and hides the start action', async () => {
    const fixture = TestBed.createComponent(CustomerPaymentBox);
    fixture.componentRef.setInput('requestId', 'request-1');
    fixture.componentRef.setInput('requestStatus', 'Completed');
    await fixture.whenStable();
    httpMock.expectOne((request) => request.url === '/api/customer/payments').flush({
      items: [payment('Processing')],
      page: 1,
      pageSize: 5,
      totalCount: 1,
      totalPages: 1,
    });
    await fixture.whenStable();

    expect(element(fixture).querySelector('a[href="/pay/payment-1"]')).not.toBeNull();
    expect(element(fixture).textContent).toContain('Awaiting payment');
    expect(element(fixture).textContent).not.toContain('Start payment');
  });

  it('does not offer payment before the job is completed', async () => {
    const fixture = TestBed.createComponent(CustomerPaymentBox);
    fixture.componentRef.setInput('requestId', 'request-1');
    fixture.componentRef.setInput('requestStatus', 'InProgress');
    await fixture.whenStable();
    httpMock.expectOne((request) => request.url === '/api/customer/payments').flush({
      items: [],
      page: 1,
      pageSize: 5,
      totalCount: 0,
      totalPages: 0,
    });
    await fixture.whenStable();

    expect(element(fixture).textContent).toContain('after the provider marks the job complete');
    expect(element(fixture).querySelector('button')).toBeNull();
  });
});
