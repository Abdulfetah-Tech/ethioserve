import { PaymentStatus } from '../core/models/payment.models';
import { paymentStatusBadge, paymentStatusLabel } from './payment-format';

const ALL: PaymentStatus[] = ['Pending', 'Processing', 'Completed', 'Failed', 'Refunded', 'Cancelled'];

describe('payment format', () => {
  it('labels every payment state', () => {
    for (const status of ALL) {
      expect(paymentStatusLabel(status).length).toBeGreaterThan(0);
    }

    expect(paymentStatusLabel('Completed')).toBe('Paid');
    expect(paymentStatusLabel('Processing')).toBe('Awaiting payment');
  });

  it('uses the established status badge styles', () => {
    expect(paymentStatusBadge('Completed')).toBe('badge badge-ok');
    expect(paymentStatusBadge('Failed')).toBe('badge badge-danger');
    expect(paymentStatusBadge('Processing')).toBe('badge badge-warn');
  });
});
