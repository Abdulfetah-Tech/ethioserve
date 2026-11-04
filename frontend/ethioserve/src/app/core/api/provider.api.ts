import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AddOfferRequest,
  MyOffer,
  MyProfile,
  ProviderAvailabilityCalendar,
  UpdateOfferRequest,
  UpdateProviderAvailabilityCalendar,
  UpdateProfileRequest,
} from '../models/provider.models';

/** Everything a provider can do with their own profile. There is no provider id anywhere: the API reads it from the token. */
@Injectable({ providedIn: 'root' })
export class ProviderApi {
  private readonly http = inject(HttpClient);

  profile(): Observable<MyProfile> {
    return this.http.get<MyProfile>('/api/providers/me');
  }

  updateProfile(request: UpdateProfileRequest): Observable<MyProfile> {
    return this.http.put<MyProfile>('/api/providers/me', request);
  }

  setAvailability(isAvailable: boolean): Observable<MyProfile> {
    return this.http.put<MyProfile>('/api/providers/me/availability', { isAvailable });
  }

  availabilityCalendar(): Observable<ProviderAvailabilityCalendar> {
    return this.http.get<ProviderAvailabilityCalendar>('/api/providers/me/availability-calendar');
  }

  updateAvailabilityCalendar(calendar: UpdateProviderAvailabilityCalendar): Observable<ProviderAvailabilityCalendar> {
    return this.http.put<ProviderAvailabilityCalendar>('/api/providers/me/availability-calendar', calendar);
  }

  clearAvailabilityCalendar(): Observable<void> {
    return this.http.delete<void>('/api/providers/me/availability-calendar');
  }

  requestVerification(): Observable<MyProfile> {
    return this.http.post<MyProfile>('/api/providers/me/verification-request', null);
  }

  offers(): Observable<MyOffer[]> {
    return this.http.get<MyOffer[]>('/api/providers/me/services');
  }

  addOffer(request: AddOfferRequest): Observable<MyOffer> {
    return this.http.post<MyOffer>('/api/providers/me/services', request);
  }

  updateOffer(id: string, request: UpdateOfferRequest): Observable<MyOffer> {
    return this.http.put<MyOffer>(`/api/providers/me/services/${encodeURIComponent(id)}`, request);
  }

  removeOffer(id: string): Observable<void> {
    return this.http.delete<void>(`/api/providers/me/services/${encodeURIComponent(id)}`);
  }
}