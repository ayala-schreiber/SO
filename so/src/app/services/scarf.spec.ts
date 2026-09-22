import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ScarfService } from './scarf';

describe('ScarfService', () => {
  let service: ScarfService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ScarfService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('loads products and resolves relative image URLs without changing absolute URLs', () => {
    const products = [
      { id: 1, name: 'Silk', price: 120, category: 'silk', imageUrl: '/images/silk.jpg' },
      { id: 2, name: 'Cotton', price: 90, category: 'cotton', imageUrl: 'https://example.com/cotton.jpg' }
    ];
    let actual: unknown;
    service.getScarves().subscribe(value => actual = value);
    const request = http.expectOne('/api/products');
    expect(request.request.method).toBe('GET');
    request.flush(products);
    expect(actual).toEqual([
      { ...products[0], imageUrl: '/images/silk.jpg', images:['/images/silk.jpg'] }, {...products[1],images:['https://example.com/cotton.jpg']}
    ]);
  });
});
