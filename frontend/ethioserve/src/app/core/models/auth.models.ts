export interface CurrentUser {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
  customerId: string | null;
  providerId: string | null;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: CurrentUser;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterCustomerRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  phoneNumber: string;
}

export interface RegisterProviderRequest {
  email: string;
  password: string;
  businessName: string;
  phoneNumber: string;
  regionId: number;
  cityId: number;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  token: string;
  newPassword: string;
}

export interface AuthCapabilities {
  passwordResetEnabled: boolean;
}

/** Must match the role names the API puts in the token. */
export const Roles = {
  Admin: 'Admin',
  Provider: 'Provider',
  Customer: 'Customer',
} as const;