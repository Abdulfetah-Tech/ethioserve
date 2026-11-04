import { PagedResult } from './marketplace.models';

export interface RequestMessage {
  id: string;
  serviceRequestId: string;
  providerId: string;
  senderUserId: string;
  body: string;
  createdAt: string;
}

export interface SendRequestMessage {
  providerId: string;
  body: string;
}

export type RequestMessagePage = PagedResult<RequestMessage>;
