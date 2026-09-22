import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';
import { AdminAuth } from './admin-auth';

@Component({
  selector: 'app-admin-home', standalone: true, imports: [RouterLink], styleUrl: './admin.css',
  template: `<section class="admin-panel dashboard" dir="rtl">
    <p class="eyebrow">SO · ניהול החנות</p>
    <h2>ברוך הבא לאזור הניהול</h2>
    <p class="welcome">שלום{{ username() ? ', '+username() : '' }}. כאן מנהלים את SO, בנחת ובמקום אחד.</p>
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
    <div class="dashboard-grid"><a class="dashboard-card" routerLink="/admin/security"><span class="card-mark" aria-hidden="true">◇</span><strong>אבטחת החשבון</strong><span>אימות דו־שלבי וקודי שחזור</span><span class="card-action">להגדרות האבטחה ←</span></a><a class="dashboard-card" routerLink="/admin/reports"><span class="card-mark" aria-hidden="true">▥</span><strong>דוח מכירות</strong><span>מטפחות שנמכרו וסיכום לפי תקופה</span><span class="card-action">לדוח ←</span></a><a class="dashboard-card" routerLink="/admin/coupons"><span class="card-mark" aria-hidden="true">%</span><strong>קופונים</strong><span>הנחות, תוקף וסכום קנייה מינימלי</span><span class="card-action">לניהול קופונים ←</span></a><a class="dashboard-card" routerLink="/admin/settings"><span class="card-mark" aria-hidden="true">⚙</span><strong>הגדרות החנות</strong><span>פרטי העסק, משלוחים והכנה לפתיחה</span><span class="card-action">להגדרות ←</span></a>
    <a class="dashboard-card featured" routerLink="/admin/products"><span class="card-mark" aria-hidden="true">◇</span><strong>מוצרים ומלאי</strong><span>הוספה, עריכה, מבצעים והסרת מטפחות</span><span class="card-action">לניהול המוצרים ←</span></a>
    <a class="dashboard-card" routerLink="/admin/orders"><span class="card-mark" aria-hidden="true">▤</span><strong>הזמנות</strong><span>הזמנות ממוספרות ופרטי מסירה</span><span class="card-action">לניהול ההזמנות ←</span></a>
    <a class="dashboard-card" routerLink="/admin/customers"><span class="card-mark" aria-hidden="true">○</span><strong>לקוחות</strong><span>חשבונות לקוחות ומספר הזמנות</span><span class="card-action">ללקוחות ←</span></a>
    </div><div class="dashboard-actions"><a class="button-link secondary" routerLink="/">חזרה לחנות ↗</a>
    <button (click)="logout()" [disabled]="busy()">{{ busy() ? 'מתנתק…' : 'התנתקות' }}</button>
    </div>
  </section>`
})
export class AdminHome {
  private auth = inject(AdminAuth);
  private router = inject(Router);
  busy = signal(false);
  error = signal('');
  username=signal('');
  constructor(){this.auth.session().subscribe({next:s=>this.username.set(s.username),error:()=>void this.router.navigateByUrl('/admin/login')});}
  logout() {
    if (this.busy()) return;
    this.busy.set(true); this.error.set('');
    this.auth.logout().pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => { void this.router.navigateByUrl('/admin/login'); },
      error: (e: HttpErrorResponse) => {
        if (e.status === 401) void this.router.navigateByUrl('/admin/login');
        else this.error.set('ההתנתקות לא הושלמה. נא לנסות שוב.');
      }
    });
  }
}
