import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MessageApi } from './message.api';

describe('MessageApi', () => {
  let api: MessageApi;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(MessageApi);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('loads one provider-specific conversation', () => {
    let bodies: string[] = [];

    api.list('request/1', 'provider-1', 2).subscribe((page) => {
      bodies = page.items.map((message) => message.body);
    });

    const request = httpMock.expectOne(
      (candidate) =>
        candidate.url === '/api/requests/request%2F1/messages' &&
        candidate.params.get('providerId') === 'provider-1' &&
        candidate.params.get('page') === '2' &&
        candidate.params.get('pageSize') === '50',
    );
    expect(request.request.method).toBe('GET');
    request.flush({ items: [{ body: 'Hello' }], page: 1, pageSize: 50, totalCount: 1, totalPages: 1 });

    expect(bodies).toEqual(['Hello']);
  });

  it('sends the selected provider id with the message', () => {
    api.send('request-1', { providerId: 'provider-1', body: 'Hello' }).subscribe();

    const request = httpMock.expectOne('/api/requests/request-1/messages');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ providerId: 'provider-1', body: 'Hello' });
    request.flush({});
  });
});
