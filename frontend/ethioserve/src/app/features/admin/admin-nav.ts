import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-admin-nav',
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="tabs" aria-label="Admin area">
      <a routerLink="/admin" routerLinkActive="active" ariaCurrentWhenActive="page" [routerLinkActiveOptions]="{ exact: true }">
        Overview
      </a>
      <a routerLink="/admin/providers" routerLinkActive="active" ariaCurrentWhenActive="page">Providers</a>
      <a routerLink="/admin/reviews" routerLinkActive="active" ariaCurrentWhenActive="page">Reviews</a>
      <a routerLink="/admin/categories" routerLinkActive="active" ariaCurrentWhenActive="page">Categories</a>
      <a routerLink="/admin/services" routerLinkActive="active" ariaCurrentWhenActive="page">Services</a>
    </nav>
  `,
})
export class AdminNav {}