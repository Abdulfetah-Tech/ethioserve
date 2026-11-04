import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { RequestMessagePage, SendRequestMessage } from '../models/message.models';

@Injectable({ providedIn: 'root' })
export class MessageApi {
  private readonly http = inject(HttpClient);

  list(requestId: string, providerId: string, page = 1, pageSize = 50): Observable<RequestMessagePage> {
    const params = new HttpParams()
      .set('providerId', providerId)
      .set('page', String(page))
      .set('pageSize', String(pageSize));

    return this.http.get<RequestMessagePage>(this.url(requestId), { params });
  }

  send(requestId: string, message: SendRequestMessage): Observable<unknown> {
    return this.http.post(this.url(requestId), message);
  }

  private url(requestId: string): string {
    return `/api/requests/${encodeURIComponent(requestId)}/messages`;
  }
}
