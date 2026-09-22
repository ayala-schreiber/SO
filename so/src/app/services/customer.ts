import {Injectable,inject,signal} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {switchMap,tap} from 'rxjs';
export interface CustomerSession {name:string;email:string;emailVerified?:boolean;}
export interface Order {reservationExpiresAt?:string|null;postalCode?:string|null;deliveryNotes?:string|null;paymentMethod?:string|null;paymentRecipient?:string|null;paymentRecipientName?:string|null;paidAt?:string|null;id:string|number;number:string;legacyNumber?:string|null;contactName:string;email:string;phone:string;address:string;pickup:boolean;items:{productId:number;name:string;color:string|null;size:string;unitPrice:number;quantity:number}[];subtotal:number;discount:number;couponCode:string|null;deliveryFee:number;total:number;status:string;fulfillmentStatus?:string;fulfillmentHistory?:{status:string;at:string}[];createdAt:string;version:number;}
@Injectable({providedIn:'root'})
export class CustomerApi {
 private http=inject(HttpClient);session=signal<CustomerSession|null>(null);
 refresh(){return this.http.get<CustomerSession>('/api/customer/session').pipe(tap(s=>this.session.set(s)));}
 post<T>(url:string,body:unknown){return this.http.get<{token:string}>('/api/customer/csrf').pipe(switchMap(({token})=>this.http.post<T>(url,body,{headers:{'X-CSRF-TOKEN':token}})));}
 login(body:{email:string;password:string}){return this.post<CustomerSession>('/api/customer/login',body).pipe(tap(s=>this.session.set(s)));}
 register(body:{email:string;password:string;name:string}){return this.post<CustomerSession>('/api/customer/register',body).pipe(tap(s=>this.session.set(s)));}
 logout(){return this.post<void>('/api/customer/logout',{}).pipe(tap(()=>this.session.set(null)));}
 put<T>(url:string,body:unknown){return this.http.get<{token:string}>('/api/customer/csrf').pipe(switchMap(({token})=>this.http.put<T>(url,body,{headers:{'X-CSRF-TOKEN':token}})));}
 // הצהרה שההעברה בוצעה. אינה מאשרת תשלום — היא מעבירה את ההזמנה לתור האישור של החנות.
 declarePaid(code:string){return this.put<Order>('/api/orders/'+code+'/paid',{});}
 orders(){return this.http.get<Order[]>('/api/customer/orders');}
 order(id:string){let key='';try{key=localStorage.getItem('so-order-access-'+id)||'';}catch{}return this.http.get<Order>('/api/orders/'+id,{headers:key?{'X-Order-Access':key}:{}}).pipe(tap(()=>{if(key)try{localStorage.removeItem('so-order-access-'+id);}catch{}}));}
}
