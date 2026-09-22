import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { AdminAuth } from './admin-auth';
import { adminGuard } from './admin-guard';
import { firstValueFrom, Observable } from 'rxjs';

describe('Admin authentication', () => {
  let auth: AdminAuth;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    auth = TestBed.inject(AdminAuth); http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  it('fetches a CSRF token before login and sends it with credentials', () => {
    let result: unknown;
    auth.login('owner', 'test-only').subscribe(value => result = value);
    http.expectNone('/api/admin/login');
    http.expectOne('/api/admin/csrf').flush({ token: 'anonymous-token' });
    const request = http.expectOne('/api/admin/login');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('anonymous-token');
    expect(request.request.body).toEqual({ username: 'owner', password: 'test-only' });
    request.flush({ username: 'owner' });
    expect(result).toEqual({ username: 'owner' });
  });
  it('gets a fresh identity-bound token before logout', () => {
    auth.logout().subscribe();
    http.expectOne('/api/admin/csrf').flush({ token: 'owner-token' });
    const request = http.expectOne('/api/admin/logout');
    expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('owner-token');
    request.flush(null);
  });
  it('rejects anonymous navigation even if the browser has previously visited admin', async () => {
    const decision = firstValueFrom(TestBed.runInInjectionContext(() => adminGuard({} as never, {} as never)) as Observable<unknown>);
    http.expectOne('/api/admin/session').flush({}, { status: 401, statusText: 'Unauthorized' });
    const result = await decision;
    expect(TestBed.inject(Router).serializeUrl(result as never)).toBe('/admin/login');
  });
  it('allows navigation only after the server confirms the session', async () => {
    const decision = firstValueFrom(TestBed.runInInjectionContext(() => adminGuard({} as never, {} as never)) as Observable<unknown>);
    http.expectOne('/api/admin/session').flush({ username: 'owner' });
    expect(await decision).toBe(true);
  });
});
