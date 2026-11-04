import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ForgotPassword } from './forgot-password';

describe('ForgotPassword', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  async function render(enabled: boolean): Promise<ComponentFixture<ForgotPassword>> {
    const fixture = TestBed.createComponent(ForgotPassword);
    fixture.detectChanges();
    httpMock.expectOne('/api/auth/capabilities').flush({ passwordResetEnabled: enabled });
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('explains that recovery is unavailable and hides the form when email is not configured', async () => {
    const fixture = await render(false);
    const element = fixture.nativeElement as HTMLElement;

    expect(element.textContent).toContain('Password recovery is unavailable');
    expect(element.querySelector('form')).toBeNull();
  });

  it('shows the request form when password recovery is enabled', async () => {
    const fixture = await render(true);
    const element = fixture.nativeElement as HTMLElement;

    expect(element.textContent).toContain('we will send you a link');
    expect(element.querySelector('form')).not.toBeNull();
  });
});
