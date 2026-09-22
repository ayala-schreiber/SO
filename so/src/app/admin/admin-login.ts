import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';
import { AdminAuth } from './admin-auth';

@Component({
  selector: 'app-admin-login', standalone: true, imports: [FormsModule, RouterLink],
  styleUrls: ['./admin.css', './admin-security.css'],
  template: `
    <section class="admin-panel" dir="rtl" aria-labelledby="login-title">
      <p class="eyebrow">SO · ניהול החנות</p>
      <h1 id="login-title">{{ factor() ? 'עוד שלב קטן, והכניסה בטוחה' : 'טוב שחזרת' }}</h1>
      @if (!factor()) {
        <p>כניסה לניהול החנות שלך.</p>
        <form #form="ngForm" (ngSubmit)="submit()">
          <label for="username">שם משתמש</label>
          <input id="username" name="username" autocomplete="username" required maxlength="100" [(ngModel)]="username" [disabled]="busy()">
          <label for="password">סיסמה</label>
          <input id="password" name="password" type="password" autocomplete="current-password" required maxlength="256" [(ngModel)]="password" [disabled]="busy()">
          <button type="submit" [disabled]="form.invalid || busy()">{{ busy() ? 'מתבצעת בדיקה…' : 'המשך לכניסה ←' }}</button>
        </form>
      } @else {
        <p>{{ recovery() ? 'יש להזין קוד שחזור אחד שנשמר בהפעלת האימות.' : 'יש להזין את הקוד שמופיע באפליקציית האימות בטלפון.' }}</p>
        <form #factorForm="ngForm" (ngSubmit)="verify()">
          <label for="factor-code">{{ recovery() ? 'קוד שחזור' : 'קוד אימות בן 6 ספרות' }}</label>
          <input id="factor-code" name="factorCode" dir="ltr" [attr.inputmode]="recovery() ? 'text' : 'numeric'" autocomplete="one-time-code" required
            [maxlength]="recovery() ? 32 : 6" [pattern]="recovery() ? '.+' : '[0-9]{6}'" [(ngModel)]="code" [disabled]="busy()">
          <button type="submit" [disabled]="factorForm.invalid || busy()">{{ busy() ? 'מתבצע אימות…' : 'כניסה לניהול החנות ←' }}</button>
        </form>
        <button class="text-action" type="button" [disabled]="busy()" (click)="toggleRecovery()">{{ recovery() ? 'חזרה לקוד מהאפליקציה' : 'אין גישה לטלפון? שימוש בקוד שחזור' }}</button>
        <button class="text-action" type="button" [disabled]="busy()" (click)="restart()">התחלת כניסה מחדש</button>
      }
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      <a class="button-link secondary" routerLink="/">חזרה לחנות</a>
    </section>`
})
export class AdminLogin {
  private auth = inject(AdminAuth);
  private router = inject(Router);
  username = ''; password = ''; code = '';
  busy = signal(false); error = signal(''); factor = signal(false); recovery = signal(false);
  submit() {
    if (this.busy() || !this.username.trim() || !this.password) return;
    this.busy.set(true); this.error.set('');
    this.auth.login(this.username.trim(), this.password).pipe(finalize(() => { this.busy.set(false); this.password = ''; })).subscribe({
      next: result => {
        if (result.requiresTwoFactor) { this.factor.set(true); this.focusCode(); }
        else void this.router.navigateByUrl(result.setupRequired ? '/admin/security' : '/admin');
      }, error: e => this.fail(e)
    });
  }
  verify() {
    if (this.busy() || !this.code.trim()) return;
    this.busy.set(true); this.error.set('');
    this.auth.verify(this.code.trim(), this.recovery()).pipe(finalize(() => { this.busy.set(false); this.code = ''; })).subscribe({
      next: () => void this.router.navigateByUrl('/admin'), error: e => this.fail(e)
    });
  }
  toggleRecovery() { this.recovery.update(v => !v); this.code = ''; this.error.set(''); this.focusCode(); }
  restart() { this.factor.set(false); this.recovery.set(false); this.code = ''; this.password = ''; this.error.set(''); }
  private focusCode() { setTimeout(() => document.getElementById('factor-code')?.focus()); }
  private fail(e: HttpErrorResponse) {
    this.error.set(e.status === 429 ? 'בוצעו ניסיונות רבים. נא להמתין דקה ולנסות שוב.' :
      (e.status === 401 && typeof e.error?.message === 'string') ? e.error.message : 'לא ניתן להשלים את הכניסה כרגע. נא לנסות שוב.');
  }
}
