import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ProviderApi } from './provider.api';

describe('ProviderApi', () => {
  let api: ProviderApi;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(ProviderApi);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('reads the profile of the signed-in provider', () => {
    api.profile().subscribe();

    const request = httpMock.expectOne('/api/providers/me');
    expect(request.request.method).toBe('GET');
    request.flush({});
  });

  it('sends profile changes with PUT', () => {
    const body = {
      businessName: 'Abebe Plumbing', description: 'Experienced plumber serving Addis Ababa.', phoneNumber: '0911556677',
      yearsOfExperience: 10, regionId: 1, cityId: 1, subCityId: null, address: 'Bole', latitude: null, longitude: null,
    };

    api.updateProfile(body).subscribe();

    const request = httpMock.expectOne('/api/providers/me');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(body);
    request.flush({});
  });

  it('switches availability with a single flag', () => {
    api.setAvailability(false).subscribe();

    const request = httpMock.expectOne('/api/providers/me/availability');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ isAvailable: false });
    request.flush({});
  });

  it('reads, updates and clears the availability calendar', () => {
    const calendar = {
      isConfigured: true,
      weeklyHours: [{ dayOfWeek: 1, windows: [{ startMinute: 540, endMinute: 1020 }] }],
      dateExceptions: [],
    };

    api.availabilityCalendar().subscribe((result) => expect(result.isConfigured).toBe(false));
    httpMock.expectOne('/api/providers/me/availability-calendar').flush({
      isConfigured: false,
      weeklyHours: [],
      dateExceptions: [],
    });

    const requestBody = { weeklyHours: calendar.weeklyHours, dateExceptions: calendar.dateExceptions };
    api.updateAvailabilityCalendar(requestBody).subscribe();
    const update = httpMock.expectOne('/api/providers/me/availability-calendar');
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toEqual(requestBody);
    update.flush(calendar);

    api.clearAvailabilityCalendar().subscribe();
    const clear = httpMock.expectOne('/api/providers/me/availability-calendar');
    expect(clear.request.method).toBe('DELETE');
    clear.flush(null);
  });

  it('requests verification without a body', () => {
    api.requestVerification().subscribe();

    const request = httpMock.expectOne('/api/providers/me/verification-request');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeNull();
    request.flush({});
  });

  it('lists, adds, updates and removes offers', () => {
    api.offers().subscribe();
    httpMock.expectOne('/api/providers/me/services').flush([]);

    api.addOffer({ serviceId: 's1', price: 300, description: null }).subscribe();
    const add = httpMock.expectOne('/api/providers/me/services');
    expect(add.request.method).toBe('POST');
    expect(add.request.body).toEqual({ serviceId: 's1', price: 300, description: null });
    add.flush({});

    api.updateOffer('o1', { price: 450, description: 'Parts included', isActive: false }).subscribe();
    const update = httpMock.expectOne('/api/providers/me/services/o1');
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toEqual({ price: 450, description: 'Parts included', isActive: false });
    update.flush({});

    api.removeOffer('o1').subscribe();
    const remove = httpMock.expectOne('/api/providers/me/services/o1');
    expect(remove.request.method).toBe('DELETE');
    remove.flush(null);
  });

  it('encodes offer ids in the URL', () => {
    api.removeOffer('a/b').subscribe();

    httpMock.expectOne('/api/providers/me/services/a%2Fb').flush(null);
  });
});