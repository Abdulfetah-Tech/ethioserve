import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AdminReviews } from './admin-reviews';

const review = {
  id: 'review-1',
  providerBusinessName: 'Abebe Repairs',
  rating: 5,
  comment: 'Arrived on time.',
  createdAt: '2026-10-30T12:00:00Z',
  isVisible: true,
};

const page = (item: typeof review) => ({
  items: [item],
  page: 1,
  pageSize: 15,
  totalCount: 1,
  totalPages: 1,
});

describe('AdminReviews', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AdminReviews],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('lists review details and refreshes the queue after hiding a review', () => {
    const fixture = TestBed.createComponent(AdminReviews);
    fixture.detectChanges();

    httpMock.expectOne((request) => request.url === '/api/admin/reviews').flush(page(review));
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Abebe Repairs');
    expect(fixture.nativeElement.textContent).toContain('Arrived on time.');

    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    expect(button.textContent).toContain('Hide review');
    button.click();
    fixture.detectChanges();

    const hide = httpMock.expectOne('/api/admin/reviews/review-1/hide');
    expect(hide.request.method).toBe('POST');
    hide.flush({ ...review, isVisible: false });

    httpMock
      .expectOne((request) => request.url === '/api/admin/reviews')
      .flush(page({ ...review, isVisible: false }));
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Review hidden.');
    expect(fixture.nativeElement.textContent).toContain('Restore review');
    fixture.destroy();
  });
});
