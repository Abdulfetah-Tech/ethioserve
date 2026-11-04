export type VerificationStatus = 'Unverified' | 'Pending' | 'Verified' | 'Rejected' | 'Suspended';

/** The signed-in provider's own view of their profile (includes private fields). */
export interface MyProfile {
  id: string;
  businessName: string;
  description: string;
  phoneNumber: string;
  yearsOfExperience: number;
  verificationStatus: VerificationStatus;
  verificationNote: string | null;
  averageRating: number;
  totalReviews: number;
  regionId: number;
  regionName: string;
  cityId: number;
  cityName: string;
  subCityId: number | null;
  subCityName: string | null;
  address: string;
  latitude: number | null;
  longitude: number | null;
  isAvailable: boolean;
  activeServiceCount: number;
}

export interface MyOffer {
  id: string;
  serviceId: string;
  serviceName: string;
  categoryId: string;
  categoryName: string;
  price: number;
  description: string | null;
  isActive: boolean;
}

export interface UpdateProfileRequest {
  businessName: string;
  description: string;
  phoneNumber: string;
  yearsOfExperience: number;
  regionId: number;
  cityId: number;
  subCityId: number | null;
  address: string;
  latitude: number | null;
  longitude: number | null;
}

export interface AddOfferRequest {
  serviceId: string;
  price: number;
  description: string | null;
}

export interface UpdateOfferRequest {
  price: number;
  description: string | null;
  isActive: boolean;
}

export interface AvailabilityWindow {
  startMinute: number;
  endMinute: number;
}

export interface WeeklyAvailabilityDay {
  dayOfWeek: number;
  windows: AvailabilityWindow[];
}

export interface AvailabilityDateException {
  date: string;
  isClosed: boolean;
  windows: AvailabilityWindow[];
}

export interface UpdateProviderAvailabilityCalendar {
  weeklyHours: WeeklyAvailabilityDay[];
  dateExceptions: AvailabilityDateException[];
}

export interface ProviderAvailabilityCalendar extends UpdateProviderAvailabilityCalendar {
  isConfigured: boolean;
}