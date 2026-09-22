import {Component,input,output} from '@angular/core';
import {DatePipe} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {Order} from '../services/customer';
import {fulfillmentLabel,nextFulfillment} from '../services/fulfillment';
import {paymentLabel} from '../services/payment-labels';

@Component({selector:'so-order-progress',standalone:true,imports:[DatePipe,FormsModule],styleUrl:'./order-progress.css',template:`
<section class="payment-card" aria-label="תשלום ההזמנה">
  <header><h3>תשלום ההזמנה</h3><span class="badge" [class.success]="order().status==='Paid'" [class.attention]="order().status==='AwaitingPaymentApproval'">{{badge()}}</span></header>
  @if(order().paymentMethod){<p>{{paymentLabel(order().paymentMethod!)}} · {{order().paymentRecipientName}} · <bdi>{{order().paymentRecipient}}</bdi></p>}
  @if(canApprove()){
    <fieldset [disabled]="busy()"><legend>אישור העברה שהתקבלה</legend>
      @if(order().status==='AwaitingPayment'){<p class="muted">הלקוחה טרם סימנה שביצעה את ההעברה. אם הכסף התקבל בכל זאת, אפשר לאשר כאן.</p>}
      @if(order().status==='Expired'){<p class="muted">ההזמנה סומנה כנטושה לאחר שלא שולמה. אם הכסף התקבל בכל זאת, אפשר לאשר כאן; הזמינות תיבדק מחדש.</p>}
      <label class="reference">אסמכתה לתשלום<input [(ngModel)]="reference" maxlength="100" placeholder="מספר האסמכתה ב־bit או ב־PayPal"></label>
      <label class="check"><input type="checkbox" [(ngModel)]="received"><span>בדקתי בחשבון שהתקבלו {{order().total}} ₪ עבור <bdi>{{order().number}}</bdi></span></label>
      <button type="button" [disabled]="busy()||!received||!reference.trim()" (click)="approve.emit(reference.trim())">אישור קבלת התשלום</button>
    </fieldset>
  }
  @if(order().paidAt){<p class="muted">אושר ב־{{order().paidAt|date:'dd/MM/yyyy HH:mm'}}</p>}
  @if(order().reservationExpiresAt && awaiting()){<p class="muted">המלאי שמור עד {{order().reservationExpiresAt|date:'dd/MM/yyyy HH:mm'}}. באישור מאוחר תיבדק הזמינות מחדש.</p>}
</section>
<section class="tracking-card" aria-label="מעקב טיפול בהזמנה">
  <header><h3>מעקב {{order().pickup?'איסוף עצמי':'משלוח'}}</h3><span class="badge">{{progressText()}}</span></header>
  <ol class="steps" aria-label="שלבי הטיפול">
    @for(step of steps();track step.status;let i=$index){
      <li [class.done]="stageIndex()>i" [class.current]="stageIndex()===i" [attr.aria-current]="stageIndex()===i?'step':null">
        <span class="marker" aria-hidden="true">{{stageIndex()>i?'✓':i+1}}</span><strong>{{step.title}}</strong>
        <small>{{stageIndex()>i?'הושלם':stageIndex()===i?(i===2?'הושלם':'בשלב זה'):'בהמשך'}}</small>
      </li>
    }
  </ol>
  <div class="next-action">
    <p role="status">{{guidance()}}</p>
    @if(next(order());as stage){<button type="button" [disabled]="busy()" (click)="advance.emit(stage)">{{actionLabel(stage)}}</button>}
  </div>
  @if(order().fulfillmentHistory?.length){<details class="history"><summary>היסטוריית עדכונים</summary><ul>@for(event of order().fulfillmentHistory;track $index){<li><span>{{label(event.status)}}</span><time>{{event.at|date:'dd/MM/yyyy HH:mm'}}</time></li>}</ul></details>}
</section>
`})
export class OrderProgress {
  order=input.required<Order>();busy=input(false);approve=output<string>();advance=output<string>();
  reference='';received=false;paymentLabel=paymentLabel;label=fulfillmentLabel;next=nextFulfillment;
  awaiting(){return ['AwaitingPayment','AwaitingPaymentApproval'].includes(this.order().status);}
  // גם הזמנה שסומנה כנטושה ניתנת לאישור, כדי שכסף שהתקבל באיחור לא ייתקע.
  canApprove(){return this.awaiting()||this.order().status==='Expired';}
  // ההבחנה בין "הלקוחה סימנה שהעבירה" לבין "טרם סימנה" היא מה שמאפשר לדעת במה לטפל קודם.
  badge(){switch(this.order().status){case 'Paid':return 'התשלום אושר';case 'Cancelled':return 'ההזמנה בוטלה';
   case 'Expired':return 'פג תוקף ללא תשלום';
   case 'AwaitingPaymentApproval':return 'הלקוחה סימנה שהעבירה — לבדיקה';default:return 'ממתינה להעברה מהלקוחה';}}
  steps(){return [{status:'Preparing',title:'הכנת ההזמנה'},{status:this.order().pickup?'ReadyForPickup':'OutForDelivery',title:this.order().pickup?'מוכן לאיסוף':'יצא למשלוח'},{status:this.order().pickup?'Collected':'Delivered',title:this.order().pickup?'נאסף':'נמסר'}];}
  stageIndex(){return this.order().status==='Paid'?this.steps().findIndex(s=>s.status===this.order().fulfillmentStatus):-1;}
  progressText(){const i=this.stageIndex();return this.order().status==='Cancelled'?'בוטלה':i<0?'טרם התחיל':i===2?'הושלם · 3 מתוך 3':'שלב '+(i+1)+' מתוך 3';}
  guidance(){if(this.order().status==='Cancelled')return 'ההזמנה בוטלה.';if(this.order().status!=='Paid')return 'הטיפול בהזמנה יתחיל לאחר אישור התשלום.';switch(this.order().fulfillmentStatus){case 'Pending':return 'התשלום אושר. אפשר להתחיל בשלב 1 מתוך 3: הכנת ההזמנה.';case 'Preparing':return this.order().pickup?'לאחר סיום האריזה, יש לסמן שההזמנה מוכנה לאיסוף.':'לאחר מסירת ההזמנה למשלוח, יש לעדכן את השלב הבא.';case 'ReadyForPickup':return 'ההזמנה מוכנה. יש לתאם איסוף ולסמן ״נאסף״ רק לאחר המסירה.';case 'OutForDelivery':return 'ההזמנה בדרך. יש לסמן ״נמסר״ לאחר המסירה ללקוח או ללקוחה.';case 'Collected':return 'האיסוף הושלם. תודה על הטיפול בהזמנה.';case 'Delivered':return 'המשלוח נמסר. תודה על הטיפול בהזמנה.';default:return this.label(this.order().fulfillmentStatus);}}
  actionLabel(stage:string){return ({Preparing:'התחלת הכנת ההזמנה',ReadyForPickup:'סימון כמוכן לאיסוף',OutForDelivery:'סימון כיצא למשלוח',Collected:'אישור שההזמנה נאספה',Delivered:'אישור שההזמנה נמסרה'} as Record<string,string>)[stage]||this.label(stage);}
}
