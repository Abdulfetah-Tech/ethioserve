import { MyOffer, MyProfile, VerificationStatus } from './provider.models';

export interface AdminProviderListItem {
  id: string;
  businessName: string;
  phoneNumber: string;
  cityName: string;
  verificationStatus: VerificationStatus;
  verificationNote: string | null;
  averageRating: number;
  totalReviews: number;
  activeServiceCount: number;
  createdAt: string;
  verificationReviewedAt: string | null;
}

/** The profile and offers have the same shape the provider sees of themselves; the admin also gets the owner and dates. */
export interface AdminProviderDetail {
  profile: MyProfile;
  userId: string;
  createdAt: string;
  verificationReviewedAt: string | null;
  offers: MyOffer[];
}

export interface AdminReview {
  id: string;
  providerBusinessName: string;
  rating: number;
  comment: string | null;
  createdAt: string;
  isVisible: boolean;
}

export type StatusFilter = VerificationStatus | 'All';

export interface AdminProviderQuery {
  status?: StatusFilter;
  search?: string;
  page?: number;
}

export interface AdminReviewQuery {
  isVisible?: boolean;
  page?: number;
}