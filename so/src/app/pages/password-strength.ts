import { Component, input } from '@angular/core';
import { passwordAccepts, passwordPolicyMessage } from '../services/password-policy';

@Component({selector:'app-password-strength',standalone:true,template:`
 <div class="password-strength">
  <p>{{ hint }}</p>
  @if (password()) {
   <meter min="0" max="3" low="2" high="3" optimum="3" [value]="score()" aria-label="חוזק סיסמה משוער"></meter>
   <p aria-live="polite">{{ label() }}</p>
  }
 </div>`,styles:[`.password-strength{margin:8px 0 18px;color:#65514a;font-size:14px}.password-strength p{margin:6px 0}.password-strength meter{display:block;width:100%;height:12px}`]})
export class PasswordStrength {
 password=input(''); hint=passwordPolicyMessage;
 score(){return !passwordAccepts(this.password())?1:this.password().length<16?2:3;}
 label(){return this.score()===1?'הסיסמה חלשה או אינה עומדת בדרישות':this.score()===2?'הסיסמה עומדת בדרישות. משפט ארוך יותר עדיף.':'אורך טוב למשפט סיסמה. כדאי להימנע משמות וממשפטים מוכרים.';}
}
