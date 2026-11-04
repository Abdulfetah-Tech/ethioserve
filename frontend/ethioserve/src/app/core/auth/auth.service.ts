import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, finalize, firstValueFrom, map, of, shareReplay, tap } from 'rxjs';
import {
  AuthCapabilities,
  AuthResponse,
  CurrentUser,
  ForgotPasswordRequest,
  LoginRequest,
  RegisterCustomerRequest,
  RegisterProviderRequest,
  ResetPasswordRequest,
} from '../models/auth.models';

// The API requires this header on refresh and logout: a cross-site form cannot send it (CSRF defence).
const CSRF_HEADER = { 'X-Requested-With': 'XMLHttpRequest' };

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly tokenState = signal<string | null>(null);
  private readonly userState = signal<CurrentUser | null>(null);

  // The one refresh call currently in flight, shared by everyone who needs a new token at the same time.
  private refreshInFlight: Observable<string> | null = null;

  /** Kept in memory only: never written to localStorage or sessionStorage. */
  readonly accessToken = this.tokenState.asReadonly();
  readonly currentUser = this.userState.asReadonly();
  readonly isAuthenticated = computed(() => this.userState() !== null);
  readonly roles = computed(() => this.userState()?.roles ?? []);

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }

  login(request: LoginRequest): Observable<CurrentUser> {
    return this.http.post<AuthResponse>('/api/auth/login', request).pipe(map((r) => this.accept(r)));
  }

  registerCustomer(request: RegisterCustomerRequest): Observable<CurrentUser> {
    return this.http
      .post<AuthResponse>('/api/auth/register/customer', request)
      .pipe(map((r) => this.accept(r)));
  }

  registerProvider(request: RegisterProviderRequest): Observable<CurrentUser> {
    return this.http
      .post<AuthResponse>('/api/auth/register/provider', request)
      .pipe(map((r) => this.accept(r)));
  }

  getCapabilities(): Observable<AuthCapabilities> {
    return this.http.get<AuthCapabilities>('/api/auth/capabilities');
  }

  forgotPassword(request: ForgotPasswordRequest): Observable<unknown> {
    return this.http.post('/api/auth/forgot-password', request);
  }

  resetPassword(request: ResetPasswordRequest): Observable<unknown> {
    return this.http.post('/api/auth/reset-password', request);
  }

  /**
   * Gets a new access token using the httpOnly refresh cookie. Concurrent callers share ONE request, so a burst of
   * expired-token errors can never trigger a burst of refreshes (which would look like token theft to the server).
   */
  refresh(): Observable<string> {
    if (!this.refreshInFlight) {
      this.refreshInFlight = this.http
        .post<AuthResponse>('/api/auth/refresh-token', null, { headers: CSRF_HEADER })
        .pipe(
          map((response) => {
            this.accept(response);
            return response.accessToken;
          }),
          finalize(() => {
            this.refreshInFlight = null;
          }),
          shareReplay({ bufferSize: 1, refCount: false }),
        );
    }

    return this.refreshInFlight;
  }

  /** Called once at startup: if a valid refresh cookie exists, the user is signed in again without typing anything. */
  restoreSession(): Promise<void> {
    return firstValueFrom(
      this.refresh().pipe(
        map((): void => undefined),
        catchError(() => of<void>(undefined)), // no cookie or an expired session is normal, not an error
      ),
    );
  }

  logout(): Observable<void> {
    return this.http.post<void>('/api/auth/logout', null, { headers: CSRF_HEADER }).pipe(
      catchError(() => of<void>(undefined)), // sign out locally even if the server cannot be reached
      tap(() => this.clearSession()),
    );
  }

  clearSession(): void {
    this.tokenState.set(null);
    this.userState.set(null);
  }

  private accept(response: AuthResponse): CurrentUser {
    this.tokenState.set(response.accessToken);
    this.userState.set(response.user);

    return response.user;
  }
}