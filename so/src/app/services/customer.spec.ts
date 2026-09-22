import {TestBed} from '@angular/core/testing';
import {provideHttpClient} from '@angular/common/http';
import {provideHttpClientTesting,HttpTestingController} from '@angular/common/http/testing';
import {CustomerApi} from './customer';

describe('Declaring a manual transfer',()=>{
 let http:HttpTestingController;let api:CustomerApi;
 beforeEach(()=>{TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting()]});
  http=TestBed.inject(HttpTestingController);api=TestBed.inject(CustomerApi);});
 afterEach(()=>http.verify());

 it('puts to the public order code with a freshly fetched token',()=>{
  let received:{status:string}|null=null;
  api.declarePaid('SO-AB23CD45').subscribe(o=>received=o as {status:string});
  http.expectOne('/api/customer/csrf').flush({token:'fresh-token'});
  const request=http.expectOne('/api/orders/SO-AB23CD45/paid');
  expect(request.request.method).toBe('PUT');
  expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('fresh-token');
  request.flush({number:'SO-AB23CD45',status:'AwaitingPaymentApproval'});
  expect(received!.status).toBe('AwaitingPaymentApproval');
 });

 it('never sends the declaration when the token request fails',()=>{
  let failed=false;
  api.declarePaid('SO-AB23CD45').subscribe({error:()=>failed=true});
  http.expectOne('/api/customer/csrf').flush({},{status:503,statusText:'Service Unavailable'});
  http.expectNone('/api/orders/SO-AB23CD45/paid');
  expect(failed).toBe(true);
 });
});
