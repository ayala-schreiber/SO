import {Component,inject,signal} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {RouterLink} from '@angular/router';
import {HttpClient} from '@angular/common/http';
import {finalize} from 'rxjs';
interface Report{paidOrderCount:number;pendingOrderCount:number;cancelledOrderCount:number;paidUnits:number;productRevenue:number;couponDiscounts:number;shippingRevenue:number;paidTotal:number;products:{productId:number;name:string;color:string|null;paidUnits:number;productValueBeforeCoupons:number;pendingUnits:number}[];}
function localDate(d:Date){return new Date(d.getTime()-d.getTimezoneOffset()*60000).toISOString().slice(0,10);}
@Component({standalone:true,imports:[FormsModule,RouterLink],styleUrls:['./admin.css','./reports.css'],template:`
<section class="inventory" dir="rtl"><a routerLink="/admin">חזרה לניהול</a><h2>דוח מכירות</h2><p>הדוח כולל רק הזמנות שסומנו כשולמו לאחר אישור תשלום. כרגע הסליקה אינה מחוברת, ולכן הזמנות ממתינות מוצגות בנפרד.</p>
<form class="report-filters" (ngSubmit)="load()"><label>מתאריך<input type="date" name="from" [(ngModel)]="from" required></label><label>עד תאריך<input type="date" name="to" [(ngModel)]="to" required></label><button [disabled]="busy()">הצגת הדוח</button></form>
@if(message()){<p role="status">{{message()}}</p>}
@if(report();as r){<div class="report-stats"><div><span>הזמנות ששולמו</span><strong>{{r.paidOrderCount}}</strong></div><div><span>מטפחות שנמכרו</span><strong>{{r.paidUnits}}</strong></div><div><span>מכירות מוצרים לאחר קופונים</span><strong>{{r.productRevenue}} ₪</strong></div><div><span>תקבולים כולל משלוח</span><strong>{{r.paidTotal}} ₪</strong></div></div>
<p>הנחות קופונים: {{r.couponDiscounts}} ₪ · דמי משלוח: {{r.shippingRevenue}} ₪</p><p>ממתינות לתשלום: {{r.pendingOrderCount}} · בוטלו: {{r.cancelledOrderCount}}</p>
@if(!r.paidOrderCount){<p class="inventory-row">אין בתקופה הזו רכישות ששולמו. עדיין אין בסיס לדירוג ״הכי נמכר״. הזמנות ממתינות אינן מכירות.</p>}
<label>מיון<select [(ngModel)]="sort"><option value="most">מהנמכר ביותר</option><option value="least">מהנמכר פחות</option></select></label>
<div class="table-scroll"><table><caption>מטפחות לפי מכירות בתקופה שנבחרה</caption><thead><tr><th scope="col">מטפחת וצבע</th><th scope="col">יחידות ששולמו</th><th scope="col">סכום לפני קופונים</th><th scope="col">יחידות בהזמנות ממתינות</th></tr></thead><tbody>@for(p of sorted(r);track p.productId){<tr><th scope="row">{{p.name}} {{p.color}}</th><td>{{p.paidUnits}}</td><td>{{p.productValueBeforeCoupons}} ₪</td><td>{{p.pendingUnits}}</td></tr>}</tbody></table></div><p>סכומי המוצרים בטבלה הם לפני קופונים; הסיכום העליון מציג את ההכנסה אחרי ההנחות. מועדפים נשמרים בדפדפן ואינם כלולים בדוח. זהו דוח תפעולי, לא דוח חשבונאי.</p>}
</section>`})
export class SalesReport{private http=inject(HttpClient);from=localDate(new Date(Date.now()-29*86400000));to=localDate(new Date());sort='most';report=signal<Report|null>(null);busy=signal(false);message=signal('');constructor(){this.load();}sorted(r:Report){return [...r.products].sort((a,b)=>this.sort==='least'?a.paidUnits-b.paidUnits:b.paidUnits-a.paidUnits);}
 load(){if(this.busy())return;const start=new Date(this.from+'T00:00:00'),end=new Date(this.to+'T00:00:00');end.setDate(end.getDate()+1);if(!Number.isFinite(start.getTime())||!Number.isFinite(end.getTime())||start>=end){this.message.set('בחרו תאריכים תקינים.');return;}this.busy.set(true);this.message.set('');this.report.set(null);this.http.get<Report>('/api/admin/reports/sales',{params:{from:start.toISOString(),to:end.toISOString()}}).pipe(finalize(()=>this.busy.set(false))).subscribe({next:r=>this.report.set(r),error:e=>this.message.set((e.status===0?null:e.error?.message)||'לא ניתן לטעון דוח. בדקו שההתחברות פעילה.')});}}
