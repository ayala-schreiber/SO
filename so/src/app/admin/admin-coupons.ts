import {Component,inject,signal,DestroyRef} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {RouterLink} from '@angular/router';
import {HttpClient} from '@angular/common/http';
import {finalize,switchMap} from 'rxjs';
interface Coupon{isArchived?:boolean;id:number;code:string;kind:string;value:number;minimumSubtotal:number;active:boolean;expiresAt:string|null;version:number;expiresInput?:string;}
@Component({standalone:true,imports:[FormsModule,RouterLink],styleUrls:['./admin.css','./reports.css'],template:`
<section class="inventory" dir="rtl"><a routerLink="/admin">חזרה לניהול</a><h2>קופונים</h2><p>קופון אחד להזמנה. ההנחה חלה גם על מוצרי מבצע. משלוח חינם מחושב לאחר ההנחה. אין כרגע מגבלת מספר מימושים.</p>
@if(message()){<p role="status">{{message()}}</p>}
<button (click)="newCoupon()" [disabled]="busy()">קופון חדש</button><button (click)="load()" [disabled]="busy()">רענון</button>
@for(c of coupons();track c.id){@if(!c.isArchived){<form #f="ngForm" class="inventory-row" (ngSubmit)="save(c)"><h3>{{c.id?'עריכת '+c.code:'יצירת קופון'}}</h3>
<p class="coupon-timer">{{remaining(c)}}</p><label>קוד<input name="code" [(ngModel)]="c.code" required pattern="[A-Za-z0-9_-]{3,30}" maxlength="30" placeholder="אותיות באנגלית ומספרים"></label>
<label>סוג הנחה<select name="kind" [(ngModel)]="c.kind"><option value="Percent">אחוזים</option><option value="Fixed">סכום בש״ח</option></select></label>
<label>ערך ההנחה<input name="value" type="number" [(ngModel)]="c.value" required min="0.01" [max]="c.kind==='Percent'?100:1000000" step="0.01"></label>
<label>סכום קנייה מינימלי<input name="minimumSubtotal" type="number" [(ngModel)]="c.minimumSubtotal" required min="0" max="1000000" step="0.01"></label>
<label>תוקף עד (לא חובה)<input name="expiry" type="datetime-local" [(ngModel)]="c.expiresInput"></label><p>השעה לפי המכשיר שבו את עורכת.</p>
<label class="sale-toggle"><input name="active" type="checkbox" [(ngModel)]="c.active"> קופון פעיל</label>
<button [disabled]="busy()||f.invalid">שמירה</button><button type="button" class="danger-button" [disabled]="busy()" (click)="archive(c,true)">{{c.id?'מחיקת קופון מהפעילים':'ביטול טיוטה'}}</button></form>}}
<details class="inventory-row"><summary>קופונים שנמחקו — אפשרות שחזור</summary><p>קופון שנמחק לא ניתן למימוש. שחזור מחזיר אותו לעריכה כשהוא כבוי; להפעלה יש לסמן פעיל ולשמור.</p>@for(c of coupons();track c.id){@if(c.isArchived){<div class="coupon-archive"><strong>{{c.code}}</strong><button type="button" (click)="archive(c,false)" [disabled]="busy()">שחזור</button></div>}}</details>
</section>`})
export class AdminCoupons {
 private http=inject(HttpClient);coupons=signal<Coupon[]>([]);busy=signal(false);message=signal('');now=signal(Date.now());private destroy=inject(DestroyRef);constructor(){this.load();const timer=setInterval(()=>this.now.set(Date.now()),1000);this.destroy.onDestroy(()=>clearInterval(timer));}
 remaining(c:Coupon){if(!c.id)return 'הקופון ייווצר לאחר שמירה';if(!c.active)return 'הקופון כבוי';if(!c.expiresAt)return 'פעיל ללא הגבלת זמן';const seconds=Math.ceil((new Date(c.expiresAt).getTime()-this.now())/1000);if(seconds<=0)return 'פג תוקף — לא ניתן למימוש';const days=Math.floor(seconds/86400),hours=Math.floor(seconds%86400/3600),minutes=Math.floor(seconds%3600/60),secs=seconds%60;return 'זמן שנותר לפי התוקף השמור: '+days+' ימים · '+String(hours).padStart(2,'0')+':'+String(minutes).padStart(2,'0')+':'+String(secs).padStart(2,'0');}
 archive(c:Coupon,archived:boolean){if(this.busy())return;if(!c.id){this.coupons.update(rows=>rows.filter(row=>row!==c));return;}this.busy.set(true);this.http.get<{token:string}>('/api/admin/csrf').pipe(switchMap(({token})=>this.http.put<Coupon>('/api/admin/coupons/'+c.id+'/archive',{archived,version:c.version},{headers:{'X-CSRF-TOKEN':token}})),finalize(()=>this.busy.set(false))).subscribe({next:updated=>{this.coupons.update(rows=>rows.map(row=>row.id===c.id?this.editable(updated):row));this.message.set(archived?'הקופון הוסר מהפעילים. אפשר לשחזר למטה.':'הקופון שוחזר במצב כבוי. אפשר לערוך ולהפעיל.');},error:e=>this.message.set((e.status===0?null:e.error?.message)||'הפעולה לא הושלמה.')});}

 private editable(c:Coupon){let expiresInput='';if(c.expiresAt){const d=new Date(c.expiresAt);expiresInput=new Date(d.getTime()-d.getTimezoneOffset()*60000).toISOString().slice(0,16);}return {...c,expiresInput};}
 load(){this.busy.set(true);this.http.get<Coupon[]>('/api/admin/coupons?includeArchived=true').pipe(finalize(()=>this.busy.set(false))).subscribe({next:r=>this.coupons.set(r.map(c=>this.editable(c))),error:()=>this.message.set('לא ניתן לטעון קופונים. בדקו שההתחברות פעילה.')});}
 newCoupon(){if(this.coupons().some(c=>!c.id))return;this.coupons.update(r=>[{id:0,code:'',kind:'Percent',value:10,minimumSubtotal:0,active:true,expiresAt:null,version:0},...r]);}
 save(c:Coupon){if(this.busy())return;let expiry:string|null=null;if(c.expiresInput){const d=new Date(c.expiresInput);if(!Number.isFinite(d.getTime())){this.message.set('תאריך התוקף אינו תקין.');return;}expiry=d.toISOString();}
 this.busy.set(true);this.message.set('');const body={...c,expiresAt:expiry};this.http.get<{token:string}>('/api/admin/csrf').pipe(switchMap(({token})=>c.id?this.http.put<Coupon>('/api/admin/coupons/'+c.id,body,{headers:{'X-CSRF-TOKEN':token}}):this.http.post<Coupon>('/api/admin/coupons',body,{headers:{'X-CSRF-TOKEN':token}})),finalize(()=>this.busy.set(false))).subscribe({next:updated=>{this.coupons.update(rows=>rows.map(row=>row===c?this.editable(updated):row));this.message.set('הקופון נשמר.');},error:e=>this.message.set((e.status===0?null:e.error?.message)||'לא ניתן לשמור. בדקו את הערכים.')});}
}
