import {Injectable,inject,signal} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {normalizePhone,displayPhone} from './israeli-phone';
export interface StoreContact {storeName:string;businessName?:string|null;businessNumber?:string|null;contactEmail?:string|null;contactPhone?:string|null;pickupEnabled:boolean;pickupAddress:string;}
@Injectable({providedIn:'root'})
export class StoreContactService {
 private http=inject(HttpClient);details=signal<StoreContact|null>(null);
 constructor(){this.refresh();}
 refresh(){this.http.get<StoreContact>('/api/store/contact').subscribe({next:s=>this.details.set(s),error:()=>this.details.set(null)});}
 email(){return this.details()?.contactEmail?.trim()||'';}
 gmail(){return 'https://mail.google.com/mail/?view=cm&fs=1&to='+encodeURIComponent(this.email());}
 mailto(){return 'mailto:'+encodeURIComponent(this.email());}
 phone(){return normalizePhone(this.details()?.contactPhone)||'';}
 phoneLabel(){return displayPhone(this.phone());}
 whatsapp(){return this.phone()?'https://wa.me/972'+this.phone().slice(1):'';}
}
