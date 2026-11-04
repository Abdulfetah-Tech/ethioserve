import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AdminProviderDetail,
  AdminProviderListItem,
  AdminProviderQuery,
  AdminReview,
  AdminReviewQuery,
} from '../models/admin.models';
import { PagedResult } from '../models/marketplace.models';

@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly http = inject(HttpClient);

  providers(query: AdminProviderQuery, pageSize = 15): Observable<PagedResult<AdminProviderListItem>> {
    let params = new HttpParams().set('pageSize', String(pageSize));

    if (query.status && query.status !== 'All') {
      params = params.set('status', query.status);
    }

    if (query.search) {
      params = params.set('search', query.search);
    }

    if (query.page && query.page > 1) {
      params = params.set('page', String(query.page));
    }

    return this.http.get<PagedResult<AdminProviderListItem>>('/api/admin/providers', { params });
  }

  provider(id: string): Observable<AdminProviderDetail> {
    return this.http.get<AdminProviderDetail>(this.url(id));
  }

  approve(id: string): Observable<AdminProviderDetail> {
    return this.http.post<AdminProviderDetail>(this.url(id, 'approve'), null);
  }

  reject(id: string, reason: string): Observable<AdminProviderDetail> {
    return this.http.post<AdminProviderDetail>(this.url(id, 'reject'), { reason });
  }

  suspend(id: string, reason: string): Observable<AdminProviderDetail> {
    return this.http.post<AdminProviderDetail>(this.url(id, 'suspend'), { reason });
  }

  reinstate(id: string): Observable<AdminProviderDetail> {
    return this.http.post<AdminProviderDetail>(this.url(id, 'reinstate'), null);
  }

  reviews(query: AdminReviewQuery, pageSize = 15): Observable<PagedResult<AdminReview>> {
    let params = new HttpParams().set('pageSize', String(pageSize));

    if (query.isVisible !== undefined) {
      params = params.set('isVisible', String(query.isVisible));
    }

    if (query.page && query.page > 1) {
      params = params.set('page', String(query.page));
    }

    return this.http.get<PagedResult<AdminReview>>('/api/admin/reviews', { params });
  }

  hideReview(id: string): Observable<AdminReview> {
    return this.http.post<AdminReview>(this.reviewUrl(id, 'hide'), null);
  }

  restoreReview(id: string): Observable<AdminReview> {
    return this.http.post<AdminReview>(this.reviewUrl(id, 'restore'), null);
  }

  private url(id: string, action?: string): string {
    const base = `/api/admin/providers/${encodeURIComponent(id)}`;

    return action ? `${base}/${action}` : base;
  }

  private reviewUrl(id: string, action: 'hide' | 'restore'): string {
    return `/api/admin/reviews/${encodeURIComponent(id)}/${action}`;
  }
}