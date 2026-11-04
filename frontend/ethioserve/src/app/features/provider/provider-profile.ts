import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { catchError, distinctUntilChanged, of, startWith, switchMap } from 'rxjs';
import { toApiError } from '../../core/api/api-error';
import { applyServerErrors } from '../../core/api/apply-server-errors';
import { LOADING, LoadState } from '../../core/api/load-state';
import { MarketplaceApi } from '../../core/api/marketplace.api';
import { ProviderApi } from '../../core/api/provider.api';
import { PHONE_PATTERN } from '../../core/auth/validators';
import { City, Region, SubCity } from '../../core/models/marketplace.models';
import { MyProfile, UpdateProfileRequest } from '../../core/models/provider.models';
import { ProviderNav } from './provider-nav';
import { ProviderAvailability } from './provider-availability';
import { MIN_DESCRIPTION_LENGTH } from './verification';

@Component({
  selector: 'app-provider-profile',
  imports: [ReactiveFormsModule, ProviderNav, ProviderAvailability],
  template: `
    <div class="stack">
      <header>
        <h1>Your profile</h1>
        <p class="muted">This is what customers see on your public page, apart from your phone number and address.</p>
      </header>

      <app-provider-nav />

      @let s = state();
      @if (s.kind === 'loading') {
        <p class="muted" role="status">Loading your profile...</p>
      } @else if (s.kind === 'error') {
        <p class="alert alert-error" role="alert">{{ s.message }}</p>
      } @else {
        @if (errorMessage(); as message) {
          <p class="alert alert-error" role="alert">{{ message }}</p>
        }
        @if (saved()) {
          <p class="alert alert-success" role="status">Your profile has been saved.</p>
        }

        <form class="card" [formGroup]="form" (ngSubmit)="save(s.data)" novalidate>
          <div class="field">
            <label for="businessName">Business or professional name</label>
            <input
              id="businessName"
              formControlName="businessName"
              [attr.aria-invalid]="form.controls.businessName.touched && form.controls.businessName.invalid"
            />
            @if (form.controls.businessName.touched && form.controls.businessName.invalid) {
              <p class="field-error">{{ form.controls.businessName.errors?.['server'] ?? 'Enter your business name.' }}</p>
            }
          </div>

          <div class="grid-2">
            <div class="field">
              <label for="phoneNumber">Phone number</label>
              <input
                id="phoneNumber"
                type="tel"
                inputmode="tel"
                formControlName="phoneNumber"
                [attr.aria-invalid]="form.controls.phoneNumber.touched && form.controls.phoneNumber.invalid"
              />
              @if (form.controls.phoneNumber.touched && form.controls.phoneNumber.invalid) {
                <p class="field-error">
                  {{ form.controls.phoneNumber.errors?.['server'] ?? 'Enter 9 to 15 digits, with no spaces.' }}
                </p>
              }
            </div>

            <div class="field">
              <label for="yearsOfExperience">Years of experience</label>
              <input
                id="yearsOfExperience"
                type="number"
                min="0"
                max="60"
                formControlName="yearsOfExperience"
                [attr.aria-invalid]="form.controls.yearsOfExperience.touched && form.controls.yearsOfExperience.invalid"
              />
              @if (form.controls.yearsOfExperience.touched && form.controls.yearsOfExperience.invalid) {
                <p class="field-error">
                  {{ form.controls.yearsOfExperience.errors?.['server'] ?? 'Enter a number from 0 to 60.' }}
                </p>
              }
            </div>
          </div>

          <div class="grid-2">
            <div class="field">
              <label for="regionId">Region</label>
              <select
                id="regionId"
                formControlName="regionId"
                (change)="onRegionChange()"
                [attr.aria-invalid]="form.controls.regionId.touched && form.controls.regionId.invalid"
              >
                <option [ngValue]="0">Choose a region</option>
                @for (region of regions(); track region.id) {
                  <option [ngValue]="region.id">{{ region.name }}</option>
                }
              </select>
              @if (form.controls.regionId.touched && form.controls.regionId.invalid) {
                <p class="field-error">{{ form.controls.regionId.errors?.['server'] ?? 'Choose a region.' }}</p>
              }
            </div>

            <div class="field">
              <label for="cityId">City</label>
              <select
                id="cityId"
                formControlName="cityId"
                (change)="form.controls.subCityId.setValue(0)"
                [attr.aria-invalid]="form.controls.cityId.touched && form.controls.cityId.invalid"
              >
                <option [ngValue]="0">Choose a city</option>
                @for (city of cities(); track city.id) {
                  <option [ngValue]="city.id">{{ city.name }}</option>
                }
              </select>
              @if (form.controls.cityId.touched && form.controls.cityId.invalid) {
                <p class="field-error">{{ form.controls.cityId.errors?.['server'] ?? 'Choose a city.' }}</p>
              }
            </div>
          </div>

          <div class="grid-2">
            <div class="field">
              <label for="subCityId">Sub-city (optional)</label>
              <select id="subCityId" formControlName="subCityId">
                <option [ngValue]="0">None</option>
                @for (subCity of subCities(); track subCity.id) {
                  <option [ngValue]="subCity.id">{{ subCity.name }}</option>
                }
              </select>
              @if (form.controls.subCityId.errors?.['server']; as serverMessage) {
                <p class="field-error">{{ serverMessage }}</p>
              }
            </div>

            <div class="field">
              <label for="address">Address</label>
              <input
                id="address"
                formControlName="address"
                autocomplete="street-address"
                aria-describedby="address-hint"
                [attr.aria-invalid]="form.controls.address.touched && form.controls.address.invalid"
              />
              <p class="hint" id="address-hint">Private: only used to confirm appointments, never shown publicly.</p>
              @if (form.controls.address.touched && form.controls.address.invalid) {
                <p class="field-error">{{ form.controls.address.errors?.['server'] ?? 'The address is too long.' }}</p>
              }
            </div>
          </div>

          <div class="field">
            <label for="description">About your business</label>
            <textarea
              id="description"
              rows="6"
              formControlName="description"
              aria-describedby="description-hint"
              [attr.aria-invalid]="form.controls.description.touched && form.controls.description.invalid"
            ></textarea>
            <p class="hint" id="description-hint">
              Needed for verification: at least {{ minDescription }} characters. You have
              {{ form.controls.description.value.trim().length }}.
            </p>
            @if (form.controls.description.touched && form.controls.description.invalid) {
              <p class="field-error">{{ form.controls.description.errors?.['server'] ?? 'The description is too long.' }}</p>
            }
          </div>

          <button type="submit" class="btn btn-primary" [disabled]="saving()">
            {{ saving() ? 'Saving...' : 'Save profile' }}
          </button>
        </form>

        <app-provider-availability />
      }
    </div>
  `,
})
export class ProviderProfileEditor {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ProviderApi);
  private readonly market = inject(MarketplaceApi);

  protected readonly minDescription = MIN_DESCRIPTION_LENGTH;

  protected readonly state = signal<LoadState<MyProfile>>(LOADING);
  protected readonly saving = signal(false);
  protected readonly saved = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    businessName: ['', [Validators.required, Validators.maxLength(150)]],
    phoneNumber: ['', [Validators.required, Validators.pattern(PHONE_PATTERN)]],
    yearsOfExperience: [0, [Validators.required, Validators.min(0), Validators.max(60)]],
    regionId: [0, [Validators.required, Validators.min(1)]],
    cityId: [0, [Validators.required, Validators.min(1)]],
    subCityId: [0],
    address: ['', [Validators.maxLength(300)]],
    description: ['', [Validators.maxLength(2000)]],
  });

  protected readonly regions = toSignal(this.market.regions().pipe(catchError(() => of<Region[]>([]))), {
    initialValue: [] as Region[],
  });

  protected readonly cities = toSignal(
    this.form.controls.regionId.valueChanges.pipe(
      startWith(this.form.controls.regionId.value),
      distinctUntilChanged(),
      switchMap((regionId) =>
        regionId > 0 ? this.market.cities(regionId).pipe(catchError(() => of<City[]>([]))) : of<City[]>([]),
      ),
    ),
    { initialValue: [] as City[] },
  );

  protected readonly subCities = toSignal(
    this.form.controls.cityId.valueChanges.pipe(
      startWith(this.form.controls.cityId.value),
      distinctUntilChanged(),
      switchMap((cityId) =>
        cityId > 0 ? this.market.subCities(cityId).pipe(catchError(() => of<SubCity[]>([]))) : of<SubCity[]>([]),
      ),
    ),
    { initialValue: [] as SubCity[] },
  );

  constructor() {
    this.api.profile().subscribe({
      next: (profile) => {
        this.state.set({ kind: 'ready', data: profile });
        this.form.reset({
          businessName: profile.businessName,
          phoneNumber: profile.phoneNumber,
          yearsOfExperience: profile.yearsOfExperience,
          regionId: profile.regionId,
          cityId: profile.cityId,
          subCityId: profile.subCityId ?? 0,
          address: profile.address,
          description: profile.description,
        });
      },
      error: (error: unknown) => {
        const apiError = toApiError(error);
        this.state.set({ kind: 'error', message: apiError.message, httpStatus: apiError.status });
      },
    });
  }

  // Only a change made by the user clears the city, so loading the saved profile never wipes it.
  protected onRegionChange(): void {
    this.form.controls.cityId.setValue(0);
    this.form.controls.subCityId.setValue(0);
  }

  protected save(profile: MyProfile): void {
    this.saved.set(false);
    this.errorMessage.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const request: UpdateProfileRequest = {
      businessName: value.businessName.trim(),
      description: value.description.trim(),
      phoneNumber: value.phoneNumber.trim(),
      yearsOfExperience: value.yearsOfExperience,
      regionId: value.regionId,
      cityId: value.cityId,
      subCityId: value.subCityId > 0 ? value.subCityId : null,
      address: value.address.trim(),
      latitude: profile.latitude,
      longitude: profile.longitude,
    };

    this.saving.set(true);

    this.api.updateProfile(request).subscribe({
      next: (updated) => {
        this.state.set({ kind: 'ready', data: updated });
        this.saved.set(true);
        this.saving.set(false);
      },
      error: (error: unknown) => {
        const apiError = toApiError(error);
        const unmatched = applyServerErrors(this.form, apiError);
        const hasFieldErrors = Object.keys(apiError.fieldErrors).length > 0;

        this.errorMessage.set(hasFieldErrors ? (unmatched.join(' ') || null) : apiError.message);
        this.saving.set(false);
      },
    });
  }
}