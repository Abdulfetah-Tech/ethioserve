import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { toApiError } from '../../core/api/api-error';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-forgot-password',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="card narrow">
      <h1>Forgot your password?</h1>

      @if (loadingCapabilities()) {
        <p class="muted" role="status">Checking password recovery availability...</p>
      } @else if (capabilityError(); as message) {
        <p class="alert alert-error" role="alert">{{ message }}</p>
        <button type="button" class="btn btn-primary" (click)="loadCapabilities()">Retry</button>
        <p class="form-links"><a routerLink="/login">Back to sign in</a></p>
      } @else if (!passwordResetEnabled()) {
        <p class="muted" role="status">Password recovery is unavailable because email delivery is not configured.</p>
        <p class="form-links"><a routerLink="/login">Back to sign in</a></p>
      } @else if (sent()) {
        <p class="alert alert-success" role="status">
          If an account with that email exists, we have sent a link to reset your password. The link is valid for 60
          minutes.
        </p>
        <p class="form-links"><a routerLink="/login">Back to sign in</a></p>
      } @else {
        <p class="muted">Enter your email and we will send you a link to choose a new password.</p>

        @if (errorMessage(); as message) {
          <p class="alert alert-error" role="alert">{{ message }}</p>
        }

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <div class="field">
            <label for="email">Email</label>
            <input
              id="email"
              type="email"
              formControlName="email"
              autocomplete="email"
              [attr.aria-invalid]="form.controls.email.touched && form.controls.email.invalid"
            />
            @if (form.controls.email.touched && form.controls.email.invalid) {
              <p class="field-error">Enter a valid email address.</p>
            }
          </div>

          <button type="submit" class="btn btn-primary btn-block" [disabled]="submitting()">
            {{ submitting() ? 'Sending...' : 'Send reset link' }}
          </button>
        </form>

        <p class="form-links"><a routerLink="/login">Back to sign in</a></p>
      }
    </section>
  `,
})
export class ForgotPassword {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);

  protected readonly loadingCapabilities = signal(true);
  protected readonly passwordResetEnabled = signal(false);
  protected readonly capabilityError = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly sent = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  constructor() {
    this.loadCapabilities();
  }

  protected loadCapabilities(): void {
    this.loadingCapabilities.set(true);
    this.capabilityError.set(null);

    this.auth.getCapabilities().subscribe({
      next: (capabilities) => {
        this.passwordResetEnabled.set(capabilities.passwordResetEnabled);
        this.loadingCapabilities.set(false);
      },
      error: (error: unknown) => {
        this.capabilityError.set(toApiError(error).message);
        this.loadingCapabilities.set(false);
      },
    });
  }

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    this.auth.forgotPassword(this.form.getRawValue()).subscribe({
      // The API answers identically whether or not the email exists, and so does this page.
      next: () => this.sent.set(true),
      error: (error: unknown) => {
        this.errorMessage.set(toApiError(error).message);
        this.submitting.set(false);
      },
    });
  }
}