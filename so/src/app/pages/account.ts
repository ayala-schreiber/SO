import {PasswordStrength} from './password-strength';
import {passwordAccepts,passwordPolicyMessage} from '../services/password-policy';
import {orderStatus} from '../services/payment-labels';
import {Component,inject,signal} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {RouterLink} from '@angular/router';
import {DatePipe} from '@angular/common';
import {finalize} from 'rxjs';
import {CustomerApi,Order} from '../services/customer';
@Component({standalone:true,imports:[FormsModule,RouterLink,DatePipe,PasswordStrength],styleUrl:'../admin/admin.css',template:`
<section class="admin-panel customer-panel" dir="rtl"><p class="eyebrow">SO · החשבון שלי</p>
@if(loading()){<p role="status">טוענים את החשבון…</p>} @else if(api.session();as me){
<h1>שלום, {{me.name}}</h1><p>{{me.email}}</p><button (click)="logout()" [disabled]="busy()">התנתקות</button>
<p>{{me.emailVerified?'כתובת המייל אומתה ✓':'כתובת המייל עדיין לא אומתה'}}</p>@if(!me.emailVerified){<button type="button" (click)="verify()" [disabled]="busy()">שליחת קישור לאימות המייל</button>}
<h2>ההזמנות שלי</h2><p>מוצגות עד 100 ההזמנות האחרונות. הזמנה ממתינה אינה אישור תשלום. זמן שמירת המלאי מופיע בפרטי ההזמנה.</p>
@for(o of orders();track o.id){<a class="inventory-row" [routerLink]="['/orders',o.id]"><strong>{{o.number}}</strong><span>{{o.createdAt|date:'dd/MM/yyyy HH:mm'}} · {{o.total}} ₪</span><span>{{statusLabel(o.status)}}</span></a>}
@if(!orders().length){<p>אין עדיין הזמנות בחשבון שלך.</p>}
} @else {
<h1>{{registering?'יצירת חשבון':'טוב שחזרת'}}</h1><p class="required-note"><span class="required-mark" aria-hidden="true">*</span> שדה חובה</p><form #f="ngForm" (ngSubmit)="submit()">
@if(registering){<label><span>שם מלא <span class="required-mark" aria-hidden="true">*</span></span><input name="name" [(ngModel)]="name" required maxlength="150" autocomplete="name"></label>}
<label><span>מייל <span class="required-mark" aria-hidden="true">*</span></span><input name="email" type="email" email [(ngModel)]="email" required maxlength="254" autocomplete="email"></label>
<label><span>סיסמה <span class="required-mark" aria-hidden="true">*</span></span><input name="password" type="password" [(ngModel)]="password" required [minlength]="registering?12:1" maxlength="256" [attr.autocomplete]="registering?'new-password':'current-password'"></label>
@if(registering){<app-password-strength [password]="password"/>}
<button [disabled]="busy()||f.invalid||(registering&&!passwordAccepts(password))">{{busy()?'רגע…':registering?'יצירת חשבון':'כניסה לחשבון'}}</button></form>
<a routerLink="/forgot-password">שכחתי סיסמה</a><button class="button-link secondary" (click)="registering=!registering;message.set('')" [disabled]="busy()">{{registering?'כבר יש לך חשבון? כניסה':'אין לך חשבון? הרשמה'}}</button><p>אפשר גם להמשיך להזמנה כאורח, ללא הרשמה.</p>
}
@if(message()){<p role="status">{{message()}}</p>}
<a class="button-link secondary" routerLink="/favorites">המועדפים שלי</a><a routerLink="/catalog">חזרה למטפחות</a></section>`})
export class Account {passwordAccepts=passwordAccepts;statusLabel=orderStatus;
 api=inject(CustomerApi);loading=signal(true);busy=signal(false);message=signal('');orders=signal<Order[]>([]);registering=false;name='';email='';password='';
 constructor(){this.api.refresh().pipe(finalize(()=>this.loading.set(false))).subscribe({next:()=>this.loadOrders(),error:e=>{this.api.session.set(null);if(e.status!==401)this.message.set('לא ניתן להתחבר לשרת כרגע. נסו שוב.');}});}
 verify(){if(this.busy())return;this.busy.set(true);this.api.post<{message:string}>('/api/customer/send-verification',{}).pipe(finalize(()=>this.busy.set(false))).subscribe({next:r=>this.message.set(r.message),error:e=>this.message.set((e.status===0?null:e.error?.message)||'לא ניתן לשלוח קישור כרגע.')});}
 loadOrders(){this.api.orders().subscribe({next:rows=>this.orders.set(rows),error:()=>this.message.set('לא ניתן לטעון את ההזמנות כרגע. רעננו את העמוד.')});}
 submit(){if(this.busy())return;if(this.registering&&!passwordAccepts(this.password)){this.message.set(passwordPolicyMessage);return;}this.busy.set(true);this.message.set('');(this.registering?this.api.register({name:this.name,email:this.email,password:this.password}):this.api.login({email:this.email,password:this.password})).pipe(finalize(()=>{this.busy.set(false);this.password='';})).subscribe({next:()=>this.loadOrders(),error:e=>this.message.set(e.status===429?'בוצעו ניסיונות רבים. המתינו דקה ונסו שוב.':(e.status===0?null:e.error?.message)||'לא ניתן להשלים את הבקשה. בדקו את הפרטים.')});}
 logout(){if(this.busy())return;this.busy.set(true);this.api.logout().pipe(finalize(()=>this.busy.set(false))).subscribe({next:()=>this.orders.set([]),error:()=>this.message.set('לא ניתן להתנתק כרגע. נסו שוב.')});}
}
