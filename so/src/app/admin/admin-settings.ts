import {LaunchReadiness} from './launch-readiness';
import {StoreContactService} from '../services/store-contact';
import {IsraeliPhoneValidator} from '../services/israeli-phone';
import {ManualPaymentSettings} from './manual-payment-settings';
import {Component,inject,signal} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {RouterLink} from '@angular/router';
import {HttpClient,HttpErrorResponse} from '@angular/common/http';
import {finalize,switchMap} from 'rxjs';
interface Settings {pickupWhatsAppUrl?:string|null;reservationMinutes:number;storeName:string;businessName:string|null;businessNumber:string|null;contactEmail:string|null;contactPhone:string|null;deliveryFee:number;freeDeliveryAbove:number;pickupEnabled:boolean;pickupAddress:string;version:number;}
@Component({standalone:true,imports:[FormsModule,RouterLink,ManualPaymentSettings,LaunchReadiness,IsraeliPhoneValidator],styleUrl:'./admin.css',template:`
<section class="inventory" dir="rtl"><a class="button-link secondary" routerLink="/admin">חזרה לניהול</a><p class="eyebrow">SO · הגדרות החנות</p><h2>מכינים את החנות לפתיחה</h2>
<p>אפשר לשמור עכשיו את מה שכבר ידוע ולהשלים את פרטי העסק אחרי הרישום. שמירת פרטים אינה מפעילה תשלומים.</p>
@if(message()){<p role="status">{{message()}}</p>}
@if(settings();as s){<form #form="ngForm" (ngSubmit)="save()"><fieldset class="inventory-row" [disabled]="busy()"><legend>פרטי החנות והעסק</legend>
<label>שם החנות <input name="storeName" [(ngModel)]="s.storeName" required maxlength="100"></label>
<label>שם העסק הרשום (להשלמה בהמשך) <input name="businessName" [(ngModel)]="s.businessName" maxlength="150"></label>
<label>מספר עוסק (להשלמה בהמשך) <input name="businessNumber" [(ngModel)]="s.businessNumber" pattern="[0-9]{9}" maxlength="9" inputmode="numeric"></label>
<label>מייל לפניות לקוחות <input name="contactEmail" type="email" email [(ngModel)]="s.contactEmail" maxlength="200"></label>
<label>טלפון לפניות לקוחות <input name="contactPhone" type="tel" soIsraeliPhone [(ngModel)]="s.contactPhone" maxlength="20"></label>
<p>פרטי הקשר מתעדכנים באתר לאחר שמירה. פרטי העסק מוצגים בעמודי המדיניות; הזמנות קיימות אינן משתנות.</p></fieldset>
<fieldset class="inventory-row" [disabled]="busy()"><legend>משלוח ואיסוף עצמי</legend>
<label>מחיר משלוח בש״ח <input name="deliveryFee" type="number" [(ngModel)]="s.deliveryFee" required min="0" max="10000" step="0.01"></label>
<label>משלוח חינם בקנייה מסכום של <input name="freeDeliveryAbove" type="number" [(ngModel)]="s.freeDeliveryAbove" required min="0" max="1000000" step="0.01"></label>
<p>הסף מחושב לפי סכום המוצרים לאחר הנחות. גם סכום ששווה בדיוק לסף זכאי למשלוח חינם.</p>
<label class="sale-toggle"><input type="checkbox" name="pickupEnabled" [(ngModel)]="s.pickupEnabled"> אפשרות לאיסוף עצמי ללא עלות</label>
<label>כתובת האיסוף <input name="pickupAddress" [(ngModel)]="s.pickupAddress" [required]="s.pickupEnabled" maxlength="250"></label>
<label>קישור WhatsApp לתיאום איסוף (לא חובה)<input name="pickupWhatsAppUrl" type="url" [(ngModel)]="s.pickupWhatsAppUrl" maxlength="500" placeholder="https://wa.me/…"></label><p>האיסוף בתיאום מראש בלבד. כאשר הקישור ריק הוא לא מוצג ללקוחות.</p>
<p>לאחר השמירה, דמי המשלוח והאיסוף מתעדכנים בסיכום הסל בעת פתיחתו מחדש.</p></fieldset>
<fieldset class="inventory-row" [disabled]="busy()"><legend>שמירת מלאי להזמנה</legend><label>משך השמירה בדקות <input name="reservationMinutes" type="number" [(ngModel)]="s.reservationMinutes" required min="1" max="1440" step="1"></label><p>ברירת המחדל היא 30 דקות. השינוי יחול על הזמנות חדשות בלבד; הוספה לסל אינה שומרת מלאי.</p></fieldset>
<button [disabled]="busy() || form.invalid" type="submit">{{busy()?'שומרים…':'שמירת ההגדרות'}}</button></form>}
@else if(!busy()){<button (click)="load()">ניסיון טעינה נוסף</button>}
<app-manual-payment-settings /><so-launch-readiness /></section>`})
export class AdminSettings {
 private contact=inject(StoreContactService);private http=inject(HttpClient);settings=signal<Settings|null>(null);busy=signal(false);message=signal('');constructor(){this.load();}
 load(){this.busy.set(true);this.http.get<Settings>('/api/admin/settings').pipe(finalize(()=>this.busy.set(false))).subscribe({next:s=>this.settings.set(s),error:()=>this.message.set('לא ניתן לטעון את ההגדרות. בדקו שההתחברות פעילה ונסו שוב.')});}
 save(){const s=this.settings();if(!s||this.busy())return;this.busy.set(true);this.message.set('');
 const body={...s,businessName:s.businessName?.trim()||null,businessNumber:s.businessNumber?.trim()||null,contactEmail:s.contactEmail?.trim()||null,contactPhone:s.contactPhone?.trim()||null};
 this.http.get<{token:string}>('/api/admin/csrf').pipe(switchMap(({token})=>this.http.put<Settings>('/api/admin/settings',body,{headers:{'X-CSRF-TOKEN':token}})),finalize(()=>this.busy.set(false))).subscribe({next:updated=>{this.settings.set(updated);this.contact.refresh();this.message.set('ההגדרות נשמרו.');},error:(e:HttpErrorResponse)=>this.message.set(e.status===409?'ההגדרות השתנו במסך אחר. רעננו את העמוד לפני שמירה נוספת.':e.status===401?'יש להתחבר מחדש לניהול.':e.error?.message??'לא ניתן לשמור. בדקו את הפרטים ונסו שוב.')});}
}
