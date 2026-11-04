import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { authResponse, signIn, userFixture } from '../testing/fixtures';
import { AuthService } from './auth.service';

const REFRESH_URL = '/api/auth/refresh-token';
const unauthorized = { status: 401, statusText: 'Unauthorized' };

describe('AuthService', () => {
  let auth: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    auth = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('starts signed out', () => {
    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.accessToken()).toBeNull();
    expect(auth.roles()).toEqual([]);
  });

  it('keeps the access token in memory only', () => {
    auth.login({ email: 'abebe@test.local', password: 'Str0ngPassw0rd' }).subscribe();

    const request = httpMock.expectOne('/api/auth/login');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'abebe@test.local', password: 'Str0ngPassw0rd' });
    request.flush(authResponse('token-1', userFixture({ roles: ['Provider'] })));

    expect(auth.accessToken()).toBe('token-1');
    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.roles()).toEqual(['Provider']);
    expect(auth.hasRole('Provider')).toBe(true);
    expect(auth.hasRole('Admin')).toBe(false);
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });

  it('loads public authentication capabilities', () => {
    let enabled: boolean | undefined;

    auth.getCapabilities().subscribe((capabilities) => (enabled = capabilities.passwordResetEnabled));

    const request = httpMock.expectOne('/api/auth/capabilities');
    expect(request.request.method).toBe('GET');
    request.flush({ passwordResetEnabled: false });

    expect(enabled).toBe(false);
  });

  it('shares one request between concurrent refresh calls', () => {
    const tokens: string[] = [];

    auth.refresh().subscribe((token) => tokens.push(token));
    auth.refresh().subscribe((token) => tokens.push(token));

    const request = httpMock.expectOne(REFRESH_URL);
    expect(request.request.headers.get('X-Requested-With')).toBe('XMLHttpRequest');
    request.flush(authResponse('fresh-token'));

    expect(tokens).toEqual(['fresh-token', 'fresh-token']);
  });

  it('starts a new request for a refresh after the previous one has finished', () => {
    auth.refresh().subscribe();
    httpMock.expectOne(REFRESH_URL).flush(authResponse('first'));

    auth.refresh().subscribe();
    httpMock.expectOne(REFRESH_URL).flush(authResponse('second'));

    expect(auth.accessToken()).toBe('second');
  });

  it('restores the session from the refresh cookie at startup', async () => {
    const restored = auth.restoreSession();
    httpMock.expectOne(REFRESH_URL).flush(authResponse('restored-token'));
    await restored;

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.accessToken()).toBe('restored-token');
  });

  it('stays signed out, without failing, when there is no valid refresh cookie', async () => {
    const restored = auth.restoreSession();
    httpMock.expectOne(REFRESH_URL).flush({}, unauthorized);

    await expect(restored).resolves.toBeUndefined();
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('signs out locally even when the server call fails', () => {
    signIn(auth, httpMock);

    auth.logout().subscribe();
    const request = httpMock.expectOne('/api/auth/logout');
    expect(request.request.headers.get('X-Requested-With')).toBe('XMLHttpRequest');
    request.flush({}, { status: 500, statusText: 'Server Error' });

    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.accessToken()).toBeNull();
  });

  it('clears the session on demand', () => {
    signIn(auth, httpMock);

    auth.clearSession();

    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.currentUser()).toBeNull();
  });
});