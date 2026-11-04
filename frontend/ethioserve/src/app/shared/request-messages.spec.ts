import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { By } from '@angular/platform-browser';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RequestMessages } from './request-messages';

describe('RequestMessages', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  async function render(): Promise<ComponentFixture<RequestMessages>> {
    const fixture = TestBed.createComponent(RequestMessages);
    fixture.componentRef.setInput('requestId', 'request-1');
    fixture.componentRef.setInput('providerId', 'provider-1');
    fixture.componentRef.setInput('otherParticipantName', 'Alpha Plumbing');
    await fixture.whenStable();

    httpMock
      .expectOne(
        (request) =>
          request.url === '/api/requests/request-1/messages' &&
          request.params.get('providerId') === 'provider-1',
      )
      .flush({
        items: [
          {
            id: 'message-1',
            serviceRequestId: 'request-1',
            providerId: 'provider-1',
            senderUserId: 'other-user',
            body: 'Hello',
            createdAt: '2025-01-01T10:00:00Z',
          },
        ],
        page: 1,
        pageSize: 50,
        totalCount: 51,
        totalPages: 2,
      });
    await fixture.whenStable();

    return fixture;
  }

  it('renders messages and sends then reloads the provider-specific thread', async () => {
    const fixture = await render();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.textContent).toContain('Conversation with Alpha Plumbing');
    expect(element.textContent).toContain('Hello');
    expect(element.textContent).toContain('Page 1 of 2');
    Array.from(element.querySelectorAll('button'))
      .find((button) => button.textContent?.trim() === 'Older messages')
      ?.click();
    const older = httpMock.expectOne(
      (request) => request.params.get('page') === '2' && request.params.get('providerId') === 'provider-1',
    );
    older.flush({
      items: [
        {
          id: 'message-0',
          serviceRequestId: 'request-1',
          providerId: 'provider-1',
          senderUserId: 'other-user',
          body: 'Earlier message',
          createdAt: '2024-12-31T10:00:00Z',
        },
      ],
      page: 2,
      pageSize: 50,
      totalCount: 51,
      totalPages: 2,
    });
    await fixture.whenStable();
    expect(element.textContent).toContain('Page 2 of 2');
    expect(element.textContent).toContain('Earlier message');

    const textarea = element.querySelector('textarea');
    expect(textarea).not.toBeNull();
    textarea!.value = 'Thanks for the details';
    textarea!.dispatchEvent(new Event('input', { bubbles: true }));
    await fixture.whenStable();
    fixture.debugElement.query(By.css('form')).triggerEventHandler('ngSubmit', new Event('submit'));
    await fixture.whenStable();

    const send = httpMock.expectOne('/api/requests/request-1/messages');
    expect(send.request.method).toBe('POST');
    expect(send.request.body).toEqual({ providerId: 'provider-1', body: 'Thanks for the details' });
    send.flush({});
    await fixture.whenStable();

    const reload = httpMock.expectOne(
      (request) =>
        request.method === 'GET' &&
        request.url === '/api/requests/request-1/messages' &&
        request.params.get('providerId') === 'provider-1' &&
        request.params.get('page') === '1',
    );
    reload.flush({
      items: [],
      page: 1,
      pageSize: 50,
      totalCount: 0,
      totalPages: 0,
    });
    await fixture.whenStable();

    expect(element.textContent).toContain('No messages yet');
  });
});
