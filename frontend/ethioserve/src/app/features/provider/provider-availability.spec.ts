import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ProviderAvailability } from './provider-availability';

describe('ProviderAvailability', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  async function render(): Promise<ComponentFixture<ProviderAvailability>> {
    const fixture = TestBed.createComponent(ProviderAvailability);
    await fixture.whenStable();
    httpMock.expectOne('/api/providers/me/availability-calendar').flush({
      isConfigured: false,
      weeklyHours: [],
      dateExceptions: [],
    });
    await fixture.whenStable();

    return fixture;
  }

  it('creates a weekly calendar and persists its hours', async () => {
    const fixture = await render();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.textContent).toContain('No calendar is set');
    element.querySelector<HTMLButtonElement>('button')?.click();
    await fixture.whenStable();

    const monday = Array.from(element.querySelectorAll('fieldset.availability-day')).find(
      (field) => field.querySelector('legend')?.textContent?.trim() === 'Monday',
    );
    expect(monday).not.toBeNull();
    monday?.querySelector<HTMLButtonElement>('button')?.click();
    await fixture.whenStable();

    const save = Array.from(element.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Save calendar',
    );
    expect(save?.disabled).toBe(false);
    save?.click();

    const request = httpMock.expectOne('/api/providers/me/availability-calendar');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      weeklyHours: [{ dayOfWeek: 1, windows: [{ startMinute: 540, endMinute: 1020 }] }],
      dateExceptions: [],
    });
    request.flush({ isConfigured: true, ...request.request.body });
    await fixture.whenStable();

    expect(element.textContent).toContain('Your availability calendar has been saved.');
    expect(element.textContent).toContain('Africa/Addis_Ababa');
  });
});
