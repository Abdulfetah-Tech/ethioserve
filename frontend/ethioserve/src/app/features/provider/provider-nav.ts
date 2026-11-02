import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-provider-nav',
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="tabs" aria-label="Provider area">
      <a routerLink="/provider" routerLinkActive="active" ariaCurrentWhenActive="page" [routerLinkActiveOptions]="{ exact: true }">
        Overview
      </a>
      <a routerLink="/provider/requests" routerLinkActive="active" ariaCurrentWhenActive="page">Requests</a>
      <a routerLink="/provider/quotes" routerLinkActive="active" ariaCurrentWhenActive="page">My quotes</a>
      <a routerLink="/provider/appointments" routerLinkActive="active" ariaCurrentWhenActive="page">Appointments</a>
      <a routerLink="/provider/payments" routerLinkActive="active" ariaCurrentWhenActive="page">Payments</a>
      <a routerLink="/provider/profile" routerLinkActive="active" ariaCurrentWhenActive="page">Profile</a>
      <a routerLink="/provider/services" routerLinkActive="active" ariaCurrentWhenActive="page">Services</a>
    </nav>
  `,
})
export class ProviderNav {}