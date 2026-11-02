import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-customer-nav',
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="tabs" aria-label="Customer area">
      <a routerLink="/customer" routerLinkActive="active" ariaCurrentWhenActive="page" [routerLinkActiveOptions]="{ exact: true }">
        Overview
      </a>
      <a routerLink="/customer/requests" routerLinkActive="active" ariaCurrentWhenActive="page">My requests</a>
      <a routerLink="/customer/appointments" routerLinkActive="active" ariaCurrentWhenActive="page">Appointments</a>
      <a routerLink="/customer/payments" routerLinkActive="active" ariaCurrentWhenActive="page">Payments</a>
    </nav>
  `,
})
export class CustomerNav {}