import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom, Observable, of } from 'rxjs';
import { vi } from 'vitest';
import { AdminAuth } from './admin-auth';
import { AdminLogin } from './admin-login';
import { AdminSecurityPage } from './admin-security';
import { adminGuard, adminSecurityGuard } from './admin-guard';

describe('Owner second-factor routing and requests', () => {
  beforeEach(() => TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting(),provideRouter([])]}));
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('sends a recovery code with a fresh CSRF token, not a password or browser access token', () => {
    const auth=TestBed.inject(AdminAuth),http=TestBed.inject(HttpTestingController);
    auth.verify('ABCD-EFGH',true).subscribe();
    http.expectOne('/api/admin/csrf').flush({token:'fresh'});
    const request=http.expectOne('/api/admin/login/verify');
    expect(request.request.body).toEqual({recoveryCode:'ABCD-EFGH'});
    expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('fresh');request.flush({username:'owner'});
  });
  it('redirects a setup-only session away from store administration', async () => {
    const result=firstValueFrom(TestBed.runInInjectionContext(()=>adminGuard({} as never,{} as never)) as Observable<unknown>);
    TestBed.inject(HttpTestingController).expectOne('/api/admin/session').flush({username:'owner',setupRequired:true});
    expect(TestBed.inject(Router).serializeUrl(await result as never)).toBe('/admin/security');
  });
  it('allows the same setup-only session to finish enrollment', async () => {
    const result=firstValueFrom(TestBed.runInInjectionContext(()=>adminSecurityGuard({} as never,{} as never)) as Observable<unknown>);
    TestBed.inject(HttpTestingController).expectOne('/api/admin/session').flush({username:'owner',setupRequired:true});
    expect(await result).toBe(true);
  });
});

describe('Owner security screens',()=>{
  it('keeps the owner at the code step after password verification and clears the password',()=>{
    const auth={login:vi.fn(()=>of({requiresTwoFactor:true}))};
    TestBed.configureTestingModule({providers:[provideRouter([]),{provide:AdminAuth,useValue:auth}]});
    const router=TestBed.inject(Router),navigate=vi.spyOn(router,'navigateByUrl');
    const page=TestBed.runInInjectionContext(()=>new AdminLogin());page.username='owner';page.password='test password';page.submit();
    expect(page.factor()).toBe(true);expect(page.password).toBe('');expect(navigate).not.toHaveBeenCalled();
  });
  it('requires acknowledgment before leaving freshly displayed recovery codes',()=>{
    const auth={security:()=>of({enabled:false,recoveryCodesRemaining:0}),confirm:()=>of({recoveryCodes:['TEST-CODE']})};
    TestBed.configureTestingModule({providers:[provideRouter([]),{provide:AdminAuth,useValue:auth}]});
    const navigate=vi.spyOn(TestBed.inject(Router),'navigateByUrl').mockResolvedValue(true);
    const page=TestBed.runInInjectionContext(()=>new AdminSecurityPage());page.key.set('TEST-KEY');page.newCode='123456';page.confirm();
    expect(page.key()).toBe('');expect(page.newCode).toBe('');expect(page.enabled()).toBe(true);
    page.finish();expect(navigate).not.toHaveBeenCalled();
    page.saved=true;page.finish();expect(navigate).toHaveBeenCalledWith('/admin');expect(page.codes()).toEqual([]);
  });
});
