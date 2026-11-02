import { PaymentStatus } from '../core/models/payment.models';

const LABELS: Record<PaymentStatus, string> = {
  Pending: 'Payment pending',
  Processing: 'Awaiting payment',
  Completed: 'Paid',
  Failed: 'Payment failed',
  Refunded: 'Refunded',
  Cancelled: 'Cancelled',
};

const BADGES: Record<PaymentStatus, string> = {
  Pending: 'badge badge-warn',
  Processing: 'badge badge-warn',
  Completed: 'badge badge-ok',
  Failed: 'badge badge-danger',
  Refunded: 'badge',
  Cancelled: 'badge',
};

export function paymentStatusLabel(status: PaymentStatus): string {
  return LABELS[status];
}

export function paymentStatusBadge(status: PaymentStatus): string {
  return BADGES[status];
}
