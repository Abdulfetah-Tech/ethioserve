import { PagedResult } from './marketplace.models';

export type PaymentStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed' | 'Refunded' | 'Cancelled';
export type PaymentMethod = 'Mock' | 'Chapa' | 'Telebirr' | 'BankTransfer' | 'Cash';
export type PaymentOutcome = 'Succeeded' | 'Failed' | 'Cancelled';

export interface CustomerPayment {
  id: string;
  requestId: string;
  requestTitle: string;
  providerName: string;
  amount: number;
  currency: string;
  method: PaymentMethod;
  status: PaymentStatus;
  reference: string;
  failureReason: string | null;
  paidAt: string | null;
  createdAt: string;
  checkoutPath: string | null;
}

export interface ProviderPayment {
  id: string;
  requestId: string;
  requestTitle: string;
  customerName: string;
  amount: number;
  currency: string;
  status: PaymentStatus;
  reference: string;
  paidAt: string | null;
  createdAt: string;
}

export interface ProviderEarnings {
  totalReceived: number;
  paymentsReceived: number;
  awaitingPaymentCount: number;
  awaitingPaymentAmount: number;
  currency: string;
}

export type CustomerPaymentPage = PagedResult<CustomerPayment>;
export type ProviderPaymentPage = PagedResult<ProviderPayment>;
