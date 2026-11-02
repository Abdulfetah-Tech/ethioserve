import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, RouterLink } from '@angular/router';
import { of } from 'rxjs';
import { PaymentCheckout } from './payment-checkout';

@Component({ imports: [RouterLink], template: '<a routerLink="/customer/payments">Payments</a>' })
class PaymentsTarget {}

const payment = {
  id: 'payment-1',
  requestId: 'request-1',
  requestTitle: 'Repair the sink',
  providerName: 'Alpha Plumbing',
  amount: 500,
  currency: 'ETB',
  method: 'Mock',
  status: 'Processing',
  reference: 'ES-20261102-ABCDEFGH2345',
  failureReason: null,
  paidAt: null,
  createdAt: '2026-11-02T10:00:00Z',
  checkoutPath: '/pay/payment-1',
};

describe('PaymentCheckout', () => {
  let httpMock: HttpTestingController;
  let fixture: ComponentFixture<PaymentCheckout>;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'customer/payments', component: PaymentsTarget }]),
        {
          provide: ActivatedRoute,
          useValue: { paramMap: of(convertToParamMap({ id: 'payment-1' })) },
        },
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(PaymentCheckout);
    await fixture.whenStable();
  });

  afterEach(() => httpMock.verify());

  const element = (): HTMLElement => fixture.nativeElement as HTMLElement;

  it('shows demo disclosure and simulates a successful payment', async () => {
    httpMock.expectOne('/api/customer/payments/payment-1').flush(payment);
    await fixture.whenStable();

    expect(element().textContent).toContain('Demo checkout only');
    expect(element().textContent).toContain('500');

    Array.from(element().querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Simulate successful payment'))
      ?.click();

    const request = httpMock.expectOne('/api/customer/payments/payment-1/simulate');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ outcome: 'Succeeded', reason: null });
    request.flush({ ...payment, status: 'Completed', paidAt: '2026-11-02T10:05:00Z', checkoutPath: null });
    await fixture.whenStable();

    expect(element().textContent).toContain('Payment received');
    expect(element().querySelectorAll('button')).toHaveLength(0);
  });

  it('surfaces checkout errors without losing the payment details', async () => {
    httpMock.expectOne('/api/customer/payments/payment-1').flush(payment);
    await fixture.whenStable();

    Array.from(element().querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Simulate decline'))
      ?.click();
    httpMock.expectOne('/api/customer/payments/payment-1/simulate').flush(
      { title: 'Conflict', detail: 'The payment attempt is closed.' },
      { status: 409, statusText: 'Conflict' },
    );
    await fixture.whenStable();

    expect(element().textContent).toContain('The payment attempt is closed.');
    expect(element().textContent).toContain('Repair the sink');
  });
});
