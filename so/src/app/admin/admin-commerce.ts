import {OrderProgress} from './order-progress';
import {FormsModule} from '@angular/forms';
import {orderStatus,paymentLabel} from '../services/payment-labels';
import {fulfillmentLabel,nextFulfillment} from '../services/fulfillment';
import {Component,inject,signal} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {ActivatedRoute,RouterLink} from '@angular/router';
import {DatePipe} from '@angular/common';
import {finalize,switchMap} from 'rxjs';
import {Order} from '../services/customer';
@Component({standalone:true,imports:[RouterLink,DatePipe,FormsModule,OrderProgress],styleUrls:['./admin.css','./reports.css'],template:`
<section class="inventory" dir="rtl"><a routerLink="/admin">חזרה לניהול</a><h2>{{customers?'לקוחות':'הזמנות'}}</h2><button (click)="load()" [disabled]="busy()">רענון</button>
@if(message()){<p role="status">{{message()}}</p>}
@if(customers){<p>עד 200 החשבונות האחרונים. פרטי רוכשים כאורח מופיעים בהזמנות.</p>@for(c of people();track c.id){<div class="inventory-row"><strong>{{c.name}}</strong><span>{{c.email}}</span><span>הזמנות: {{c.orderCount}} · הצטרפות: {{c.createdAt|date:'dd/MM/yyyy'}}</span></div>}}
@else {<p>עד 200 ההזמנות האחרונות. בהעברה ידנית יש לבדוק קבלת תשלום ולאשר כאן. מלאי נשמר לזמן מוגבל בעת יצירת הזמנה. אחרי התפוגה תתבצע בדיקת זמינות חדשה באישור התשלום.</p>@for(o of orders();track o.id){<details class="inventory-row"><summary class="order-summary"><strong>הזמנה {{o.number}} · {{o.contactName}}</strong><span class="order-meta"><span>{{o.createdAt|date:'dd/MM/yyyy HH:mm'}}</span><span>{{units(o)}} מטפחות</span><span>{{o.total}} ₪</span><span>{{statusLabel(o.status)}}</span></span></summary><p>מזהה פנימי: {{o.id}}@if(o.legacyNumber){ · מספר קודם: {{o.legacyNumber}}}</p><p>{{o.createdAt|date:'dd/MM/yyyy HH:mm'}} · {{o.email}} · {{o.phone}}</p><p>{{o.pickup?'איסוף עצמי':'משלוח'}}: {{o.address}}</p>@if(o.postalCode){<p>מיקוד: <bdi>{{o.postalCode}}</bdi></p>}@if(o.deliveryNotes){<p style="white-space:pre-wrap;overflow-wrap:anywhere">הערות למשלוח: {{o.deliveryNotes}}</p>}<div class="table-scroll"><table><caption>מוצרים בהזמנה {{o.number}}</caption><thead><tr><th scope="col">מטפחת</th><th scope="col">מידה</th><th scope="col">כמות</th><th scope="col">מחיר יחידה</th><th scope="col">סכום שורה</th></tr></thead><tbody>@for(i of o.items;track i.productId){<tr><th scope="row">{{i.name}} {{i.color}}</th><td>{{i.size}} ס״מ</td><td>{{i.quantity}}</td><td>{{i.unitPrice}} ₪</td><td>{{i.quantity*i.unitPrice}} ₪</td></tr>}</tbody></table></div><div class="order-breakdown"><span>סכום המוצרים</span><strong>{{o.subtotal}} ₪</strong><span>הנחה {{o.couponCode}}</span><strong>{{o.discount||0}} ₪</strong><span>דמי משלוח</span><strong>{{o.deliveryFee}} ₪</strong><span>סה״כ להזמנה</span><strong>{{o.total}} ₪</strong></div><so-order-progress [order]="o" [busy]="busy()" (approve)="approvePayment(o,$event)" (advance)="advance(o,$event)" />@if(cancellable(o)){<button (click)="cancel(o)" [disabled]="busy()">ביטול הזמנה ללא תשלום</button>}</details>}}
@if(!busy()&&!message()&&!(customers?people().length:orders().length)){<p>אין עדיין {{customers?'לקוחות רשומים':'הזמנות'}}.</p>}
</section>`})
export class AdminCommerce {
 approvePayment(o:Order,reference:string){this.references[o.id]=reference;this.received[o.id]=true;this.confirm(o);}
 statusLabel=orderStatus;paymentLabel=paymentLabel;references:Record<string,string>={};received:Record<string,boolean>={};
 confirm(o:Order){if(this.busy()||!this.received[o.id]||!this.references[o.id]?.trim())return;this.busy.set(true);this.message.set('');this.http.get<{token:string}>('/api/admin/csrf').pipe(switchMap(({token})=>this.http.put<Order>('/api/admin/manual-payments/orders/'+o.id+'/confirm',{version:o.version,reference:this.references[o.id].trim(),received:true},{headers:{'X-CSRF-TOKEN':token}})),finalize(()=>this.busy.set(false))).subscribe({next:updated=>{this.orders.update(rows=>rows.map(r=>r.id===o.id?updated:r));this.message.set('התשלום אושר והמלאי עודכן. אפשר להתחיל בהכנת ההזמנה.');},error:e=>this.message.set((e.status===0?null:e.error?.message)||'לא ניתן לאשר. רעננו את ההזמנות.')});}

 private http=inject(HttpClient);private route=inject(ActivatedRoute);customers=false;orders=signal<Order[]>([]);people=signal<{id:number;name:string;email:string;createdAt:string;orderCount:number}[]>([]);busy=signal(false);message=signal('');constructor(){this.route.data.subscribe(data=>{this.customers=!!data['customers'];this.load();});}
 load(){this.busy.set(true);this.message.set('');this.http.get<any[]>('/api/admin/'+(this.customers?'customers':'orders')).pipe(finalize(()=>this.busy.set(false))).subscribe({next:rows=>this.customers?this.people.set(rows):this.orders.set(rows),error:()=>this.message.set('לא ניתן לטעון נתונים. בדקו שההתחברות פעילה.')});}
 label=fulfillmentLabel;next=nextFulfillment;
 advance(o:Order,status:string){if(this.busy())return;this.busy.set(true);this.message.set('');this.http.get<{token:string}>('/api/admin/csrf').pipe(switchMap(({token})=>this.http.put<Order>('/api/admin/orders/'+o.id+'/fulfillment',{version:o.version,status},{headers:{'X-CSRF-TOKEN':token}})),finalize(()=>this.busy.set(false))).subscribe({next:updated=>this.orders.update(rows=>rows.map(r=>r.id===o.id?updated:r)),error:e=>this.message.set((e.status===0?null:e.error?.message)||'לא ניתן לעדכן. רעננו ונסו שוב.')});}
 units(o:Order){return o.items.reduce((sum,i)=>sum+i.quantity,0);}
 // גם הזמנה שסומנה כנטושה ניתנת לביטול, כדי שאפשר יהיה לנקות את התור.
 cancellable(o:Order){return ['AwaitingPayment','AwaitingPaymentApproval','Expired'].includes(o.status);}
 cancel(o:Order){if(this.busy())return;this.busy.set(true);this.http.get<{token:string}>('/api/admin/csrf').pipe(switchMap(({token})=>this.http.put<Order>('/api/admin/orders/'+o.id+'/cancel',{version:o.version},{headers:{'X-CSRF-TOKEN':token}})),finalize(()=>this.busy.set(false))).subscribe({next:updated=>this.orders.update(rows=>rows.map(r=>r.id===o.id?updated:r)),error:()=>this.message.set('לא ניתן לבטל. רעננו את ההזמנות ונסו שוב.')});}
}
