import {TestBed} from '@angular/core/testing';
import {provideHttpClient} from '@angular/common/http';
import {provideHttpClientTesting,HttpTestingController} from '@angular/common/http/testing';
import {provideRouter} from '@angular/router';
import {AdminCoupons} from './admin-coupons';
describe('Coupon timer',()=>{
 beforeEach(()=>TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting(),provideRouter([])]}));
 it('shows remaining time and expiration from the saved expiry',()=>{const f=TestBed.createComponent(AdminCoupons),c=f.componentInstance;TestBed.inject(HttpTestingController).expectOne('/api/admin/coupons?includeArchived=true').flush([]);c.now.set(Date.parse('2026-09-07T12:00:00Z'));const coupon={id:1,code:'TEST',kind:'Fixed',value:10,minimumSubtotal:0,active:true,expiresAt:'2026-09-07T12:01:40Z',version:1};expect(c.remaining(coupon)).toContain('00:01:40');c.now.set(Date.parse('2026-09-07T12:01:40Z'));expect(c.remaining(coupon)).toContain('פג תוקף');f.destroy();});
 it('does not treat disabled or unsaved coupons as running',()=>{const f=TestBed.createComponent(AdminCoupons),c=f.componentInstance;TestBed.inject(HttpTestingController).expectOne('/api/admin/coupons?includeArchived=true').flush([]);const coupon={id:1,code:'TEST',kind:'Percent',value:10,minimumSubtotal:0,active:false,expiresAt:null,version:1};expect(c.remaining(coupon)).toBe('הקופון כבוי');expect(c.remaining({...coupon,id:0})).toContain('לאחר שמירה');f.destroy();});
});
