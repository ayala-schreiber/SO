import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';
import { AdminAuth, OwnerProof } from './admin-auth';

@Component({
  selector: 'app-admin-security', standalone: true, imports: [FormsModule, RouterLink],
  styleUrls: ['./admin.css', './admin-security.css'],
  template: `<section class="admin-panel security-panel" dir="rtl" aria-labelledby="security-title">
    <p class="eyebrow">SO · אבטחת החשבון</p>
    <h1 id="security-title">כניסה בטוחה לחנות שלך</h1>
    @if (loading()) { <p role="status">טוענים את הגדרות האבטחה…</p> }
    @else if (codes().length) {
      <h2 tabindex="-1" id="recovery-title">קודי השחזור שלך</h2>
      <p>יש לשמור את הקודים במקום בטוח, בנפרד מהטלפון. כל קוד מאפשר כניסה אחת אם אין גישה לאפליקציה. הקודים מוצגים רק עכשיו.</p>
      <ul class="recovery-codes" aria-label="קודי שחזור">@for (item of codes(); track item) { <li dir="ltr">{{ item }}</li> }</ul>
      <button type="button" class="secondary" (click)="downloadCodes()">שמירת קודי השחזור בקובץ</button>
      <label class="security-check"><input type="checkbox" [(ngModel)]="saved">שמרתי את קודי השחזור במקום בטוח</label>
      <button type="button" [disabled]="!saved" (click)="finish()">סיום וחזרה לניהול החנות ←</button>
    } @else if (key()) {
      <ol class="setup-steps">
        <li>באפליקציית האימות בטלפון יש לבחור הוספת חשבון והזנה ידנית של מפתח.</li>
        <li>שם החשבון: <b>{{ accountName() }}</b>. סוג הקוד: מבוסס זמן.</li>
        <li>יש להזין את המפתח הבא באפליקציה. תוקף ההגדרה: 10 דקות.</li>
      </ol>
      <label for="shared-key">מפתח להגדרה באפליקציה</label>
      <input id="shared-key" class="shared-key" dir="ltr" readonly [value]="key()" autocomplete="off" (focus)="selectKey($event)">
      <p class="security-hint">המפתח אישי. אין לשלוח אותו לאף אדם.</p>
      <form #confirmForm="ngForm" (ngSubmit)="confirm()">
        <label for="setup-code">הקוד בן 6 הספרות שמופיע באפליקציה</label>
        <input id="setup-code" name="setupCode" dir="ltr" inputmode="numeric" autocomplete="one-time-code" required pattern="[0-9]{6}" maxlength="6" [(ngModel)]="newCode" [disabled]="busy()">
        <button type="submit" [disabled]="confirmForm.invalid || busy()">{{ busy() ? 'מתבצע אימות…' : 'אישור והפעלת האימות' }}</button>
      </form>
      <button type="button" class="text-action" [disabled]="busy()" (click)="cancelSetup()">חזרה להגדרה</button>
    } @else {
      <p class="security-status">{{ enabled() ? 'האימות הדו־שלבי פעיל ✓' : 'להמשך ניהול החנות נדרש אימות דו־שלבי.' }}</p>
      @if (enabled()) {
        <p>בכל כניסה יש להזין סיסמה וקוד מהאפליקציה. נותרו {{ remaining() }} קודי שחזור.</p>
        <div class="security-actions">
          <button type="button" class="secondary" (click)="choose('replace')">החלפת אפליקציה או טלפון</button>
          <button type="button" class="secondary" (click)="choose('recovery')">יצירת קודי שחזור חדשים</button>
        </div>
      } @else {
        <p>יש לפתוח בטלפון אפליקציית אימות שתומכת בקודים מבוססי זמן. לאחר החיבור נשמור גם קודי שחזור לכניסה במקרה שהטלפון אינו זמין.</p>
      }
      @if (!enabled() || action()) {
        @if (action() === 'replace') { <p>האפליקציה הקיימת תמשיך לפעול עד לאישור קוד מהאפליקציה החדשה.</p> }
        @if (action() === 'recovery') { <p>יצירת קודים חדשים תבטל את קודי השחזור הקודמים.</p> }
        <form #proofForm="ngForm" (ngSubmit)="start()">
          <label for="security-password">סיסמת הניהול</label>
          <input id="security-password" name="securityPassword" type="password" autocomplete="current-password" required maxlength="256" [(ngModel)]="password" [disabled]="busy()">
          @if (enabled()) {
            <label for="security-proof">{{ useRecovery() ? 'קוד שחזור קיים' : 'קוד חדש מהאפליקציה' }}</label>
            <input id="security-proof" name="securityProof" dir="ltr" autocomplete="one-time-code" [attr.inputmode]="useRecovery() ? 'text' : 'numeric'" required [maxlength]="useRecovery() ? 32 : 6" [pattern]="useRecovery() ? '.+' : '[0-9]{6}'" [(ngModel)]="proofCode" [disabled]="busy()">
            <button type="button" class="text-action" (click)="toggleRecovery()">{{ useRecovery() ? 'שימוש בקוד מהאפליקציה' : 'שימוש בקוד שחזור' }}</button>
          }
          <button type="submit" [disabled]="proofForm.invalid || busy()">{{ busy() ? 'מתבצעת בדיקה…' : action() === 'recovery' ? 'יצירת קודי שחזור' : 'התחלת חיבור לאפליקציה' }}</button>
        </form>
      }
    }
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
    @if (!codes().length) {
      <div class="security-actions">
        @if (enabled()) { <a class="button-link secondary" routerLink="/admin">חזרה לניהול</a> }
        <a class="button-link secondary" routerLink="/">חזרה לחנות</a>
        <button type="button" class="text-action" [disabled]="busy()" (click)="logout()">התנתקות</button>
      </div>
    }
  </section>`
})
export class AdminSecurityPage {
  private auth = inject(AdminAuth); private router = inject(Router);
  loading = signal(true); busy = signal(false); enabled = signal(false); remaining = signal(0);
  action = signal<'' | 'replace' | 'recovery'>(''); useRecovery = signal(false);
  key = signal(''); accountName = signal(''); codes = signal<string[]>([]); error = signal('');
  password = ''; proofCode = ''; newCode = ''; saved = false;
  constructor() { this.auth.security().pipe(finalize(() => this.loading.set(false))).subscribe({
    next: state => { this.enabled.set(state.enabled); this.remaining.set(state.recoveryCodesRemaining); }, error: e => this.fail(e)
  }); }
  choose(action: 'replace' | 'recovery') { this.action.set(action); this.clearProof(); this.error.set(''); }
  toggleRecovery() { this.useRecovery.update(v => !v); this.proofCode = ''; }
  start() {
    if (this.busy() || !this.password || (this.enabled() && !this.proofCode.trim())) return;
    const proof: OwnerProof = { password: this.password };
    if (this.enabled()) { if (this.useRecovery()) proof.recoveryCode = this.proofCode.trim(); else proof.code = this.proofCode.trim(); }
    this.busy.set(true); this.error.set('');
    if (this.action() === 'recovery') {
      this.auth.recoveryCodes(proof).pipe(finalize(() => { this.busy.set(false); this.clearProof(); })).subscribe({
        next: result => this.showCodes(result.recoveryCodes), error: e => this.fail(e)
      });
    } else {
      this.auth.setup(proof).pipe(finalize(() => { this.busy.set(false); this.clearProof(); })).subscribe({
        next: result => { this.key.set(result.sharedKey); this.accountName.set(result.accountName); setTimeout(() => document.getElementById('shared-key')?.focus()); }, error: e => this.fail(e)
      });
    }
  }
  confirm() {
    if (this.busy() || !/^[0-9]{6}$/.test(this.newCode)) return;
    this.busy.set(true); this.error.set('');
    this.auth.confirm(this.newCode).pipe(finalize(() => { this.busy.set(false); this.newCode = ''; })).subscribe({
      next: result => { this.enabled.set(true); this.key.set(''); this.showCodes(result.recoveryCodes); }, error: e => this.fail(e)
    });
  }
  private showCodes(codes: string[]) { this.codes.set(codes); this.saved = false; setTimeout(() => document.getElementById('recovery-title')?.focus()); }
  selectKey(event: Event) { (event.target as HTMLInputElement).select(); }
  downloadCodes() {
    const url = URL.createObjectURL(new Blob(['SO — קודי שחזור לחשבון הניהול\nכל קוד מיועד לשימוש חד־פעמי. יש לשמור במקום בטוח.\n\n' + this.codes().join('\n')], { type: 'text/plain;charset=utf-8' }));
    const link = document.createElement('a'); link.href = url; link.download = 'SO-recovery-codes.txt'; link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
  finish() { if (!this.saved) return; this.codes.set([]); void this.router.navigateByUrl('/admin'); }
  cancelSetup() { this.key.set(''); this.newCode = ''; this.error.set(''); }
  private clearProof() { this.password = ''; this.proofCode = ''; }
  logout() { if (this.busy()) return; this.busy.set(true); this.auth.logout().pipe(finalize(() => this.busy.set(false))).subscribe({ next: () => void this.router.navigateByUrl('/admin/login'), error: e => this.fail(e) }); }
  private fail(e: HttpErrorResponse) { this.error.set(e.status === 429 ? 'בוצעו ניסיונות רבים. נא להמתין דקה ולנסות שוב.' : (e.status === 401 && typeof e.error?.message === 'string') ? e.error.message : 'לא ניתן להשלים את הפעולה. יש לבדוק שהכניסה עדיין פעילה ולנסות שוב.'); }
}
