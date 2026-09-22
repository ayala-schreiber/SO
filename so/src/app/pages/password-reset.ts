import {PasswordStrength} from './password-strength';
import {passwordAccepts,passwordPolicyMessage} from '../services/password-policy';
import {Component,inject,signal} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {ActivatedRoute,RouterLink} from '@angular/router';
import {finalize} from 'rxjs';
import {CustomerApi} from '../services/customer';
@Component({standalone:true,imports:[FormsModule,RouterLink,PasswordStrength],styleUrl:'../admin/admin.css',template:`
<section class="admin-panel customer-panel" dir="rtl"><h1>{{reset?'בחירת סיסמה חדשה':'שכחתי סיסמה'}}</h1>
@if(!done()){<p class="required-note"><span class="required-mark" aria-hidden="true">*</span> שדה חובה</p><form #f="ngForm" (ngSubmit)="submit()">
@if(reset){<label><span>סיסמה חדשה <span class="required-mark" aria-hidden="true">*</span></span><input type="password" name="password" [(ngModel)]="password" required minlength="12" maxlength="256" autocomplete="new-password"></label><label><span>הקלדת הסיסמה שוב <span class="required-mark" aria-hidden="true">*</span></span><input type="password" name="confirm" [(ngModel)]="confirm" required autocomplete="new-password"></label><app-password-strength [password]="password"/>}
@else{<p>הזינו את המייל של החשבון לקבלת קישור לאיפוס סיסמה.</p><label><span>מייל <span class="required-mark" aria-hidden="true">*</span></span><input name="email" type="email" email [(ngModel)]="email" required maxlength="254" autocomplete="email"></label>}
<button [disabled]="busy()||f.invalid||(reset&&!passwordAccepts(password))">{{busy()?'רגע…':reset?'שמירת סיסמה חדשה':'שליחת קישור איפוס'}}</button></form>}
@if(message()){<p role="status">{{message()}}</p>}<a routerLink="/account">חזרה לכניסה</a></section>`})
export class PasswordReset {passwordAccepts=passwordAccepts;
 private api=inject(CustomerApi);private route=inject(ActivatedRoute);reset=this.route.snapshot.data['reset']===true;email='';password='';confirm='';private token=new URLSearchParams(this.route.snapshot.fragment||'').get('token')||'';busy=signal(false);done=signal(false);message=signal('');
 submit(){if(this.busy())return;if(this.reset&&!passwordAccepts(this.password)){this.message.set(passwordPolicyMessage);return;}if(this.reset&&(this.password!==this.confirm||!this.token)){this.message.set(this.token?'הסיסמאות אינן זהות.':'הקישור אינו תקין. בקשו קישור חדש.');return;}this.busy.set(true);this.message.set('');this.api.post<{message:string}>('/api/customer/'+(this.reset?'reset-password':'forgot-password'),this.reset?{token:this.token,password:this.password}:{email:this.email}).pipe(finalize(()=>this.busy.set(false))).subscribe({next:r=>{this.message.set(r.message);this.done.set(true);this.password='';this.confirm='';if(this.reset)this.api.session.set(null);},error:e=>this.message.set(e.status===429?'המתינו דקה ונסו שוב.':(e.status===0?null:e.error?.message)||'לא ניתן להשלים את הבקשה כרגע.')});}
}
