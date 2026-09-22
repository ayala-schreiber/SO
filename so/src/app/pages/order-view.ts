import {ManualPaymentInstructions} from './manual-payment-instructions';
import {orderStatus} from '../services/payment-labels';
import {fulfillmentLabel} from '../services/fulfillment';
import {Component,inject,signal} from '@angular/core';
import {ActivatedRoute,RouterLink} from '@angular/router';
import {finalize} from 'rxjs';
import {DatePipe} from '@angular/common';
import {CustomerApi,Order} from '../services/customer';
@Component({standalone:true,imports:[RouterLink,DatePipe,ManualPaymentInstructions],styleUrl:'../admin/admin.css',template:`
<section class="admin-panel dashboard" dir="rtl"><h1>פרטי ההזמנה</h1>
@if(order();as o){<h2>{{o.number}}</h2><p>{{o.createdAt|date:'dd/MM/yyyy HH:mm'}} · {{statusLabel(o.status)}}</p>
<app-manual-payment-instructions [order]="o" [busy]="busy()" (declared)="declarePaid(o)" />@if(error()){<p class="field-error" role="alert">{{error()}}</p>}<h2>מעקב {{o.pickup?'איסוף':'משלוח'}}</h2><p>{{label(o.fulfillmentStatus)}}</p><p>הסטטוס מתעדכן על ידי החנות.</p>@for(event of o.fulfillmentHistory;track $index){<div class="inventory-row"><strong>{{label(event.status)}}</strong><span>{{event.at|date:'dd/MM/yyyy HH:mm'}}</span></div>}
@for(i of o.items;track i.productId){<div class="inventory-row"><strong>{{i.name}} {{i.color}}</strong><span>{{i.size}} ס״מ · {{i.quantity}} × {{i.unitPrice}} ₪</span></div>}
@if(o.discount){<p>קופון {{o.couponCode}}: הנחה {{o.discount}} ₪</p>}<p>כמות פריטים: {{itemCount(o)}}</p><p>סכום המוצרים: {{o.subtotal}} ₪ · משלוח: {{o.deliveryFee}} ₪</p><strong>סכום שנשמר להזמנה: {{o.total}} ₪</strong><p>{{o.pickup?'איסוף עצמי':'משלוח'}} — {{o.address}}</p>@if(o.postalCode){<p>מיקוד: <bdi>{{o.postalCode}}</bdi></p>}@if(o.deliveryNotes){<p style="white-space:pre-wrap;overflow-wrap:anywhere">הערות למשלוח: {{o.deliveryNotes}}</p>}
} @else {<p role="status">{{message()}}</p>}
<a routerLink="/account">לחשבון שלי</a><a routerLink="/catalog">לחנות</a></section>`})
export class OrderView {statusLabel=orderStatus;label=fulfillmentLabel;private api=inject(CustomerApi);private route=inject(ActivatedRoute);order=signal<Order|null>(null);message=signal('טוענים…');busy=signal(false);error=signal('');
 itemCount(o:Order){return o.items.reduce((total,item)=>total+item.quantity,0);}
 declarePaid(o:Order){if(this.busy())return;this.busy.set(true);this.error.set('');
  this.api.declarePaid(o.number).pipe(finalize(()=>this.busy.set(false))).subscribe({next:updated=>this.order.set(updated),error:e=>this.error.set((e.status===0?null:e.error?.message)||'לא ניתן לעדכן כרגע. רעננו ונסו שוב.')});}
 constructor(){this.route.paramMap.subscribe(p=>{this.order.set(null);this.api.order(p.get('id')||'0').subscribe({next:o=>this.order.set(o),error:()=>this.message.set('ההזמנה אינה זמינה. התחברו לחשבון שהזמין, או פתחו בדפדפן שבו בוצעה הזמנת האורח.')});});}}
