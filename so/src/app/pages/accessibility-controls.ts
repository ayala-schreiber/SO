import {StoreContactService} from '../services/store-contact';
import {Component,inject,signal,viewChild,ElementRef} from '@angular/core';
import {DOCUMENT} from '@angular/common';
import {RouterLink} from '@angular/router';
@Component({selector:'app-accessibility-controls',standalone:true,imports:[RouterLink],template: `
<button class="access-launch" type="button" aria-label="פתיחת אפשרויות נגישות" aria-haspopup="dialog" (click)="show()">
<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="4" r="2"/><path d="M4 8l8 2 8-2M12 10v5m0 0l-4 6m4-6l4 6"/></svg><span>נגישות</span></button>
<dialog #panel dir="rtl" aria-labelledby="access-title" (close)="open.set(false)">
<header><div><small>SO · בנוחות שלך</small><h2 id="access-title">אפשרויות נגישות</h2></div><button type="button" class="close" aria-label="סגירת אפשרויות נגישות" (click)="hide()">×</button></header>
<p>התאימי את התצוגה לנוחות הקריאה שלך.</p>
<div class="options">
<button type="button" [attr.aria-pressed]="large()" (click)="large.set(!large());apply()"><b aria-hidden="true">A+</b><span>הגדלת טקסט</span></button>
<button type="button" [attr.aria-pressed]="contrast()" (click)="contrast.set(!contrast());apply()"><b aria-hidden="true">◐</b><span>ניגודיות גבוהה</span></button>
<button type="button" [attr.aria-pressed]="links()" (click)="links.set(!links());apply()"><b aria-hidden="true"><u>A</u></b><span>הדגשת קישורים</span></button>
<button type="button" [attr.aria-pressed]="motion()" (click)="motion.set(!motion());apply()"><b aria-hidden="true">Ⅱ</b><span>צמצום תנועה ועצירת סרטון</span></button>
</div><button type="button" class="reset" (click)="reset()">איפוס ההעדפות</button>
<footer><a routerLink="/accessibility" (click)="hide()">הצהרת נגישות</a>@if(contact.phone()){<a [href]="contact.whatsapp()" target="_blank" rel="noopener">דיווח על קושי ב־WhatsApp</a>}</footer>
</dialog>`,styleUrl:'./accessibility-controls.css'})
export class AccessibilityControls{
 contact=inject(StoreContactService);
 private doc=inject(DOCUMENT);panel=viewChild<ElementRef<HTMLDialogElement>>('panel');open=signal(false);large=signal(false);contrast=signal(false);links=signal(false);motion=signal(false);
 constructor(){try{const x=JSON.parse(localStorage.getItem('so-display')||'{}');this.large.set(x.large===true);this.contrast.set(x.contrast===true);this.links.set(x.links===true);this.motion.set(x.motion===true);}catch{}this.apply();}
 show(){this.panel()?.nativeElement.showModal();this.open.set(true);}
 hide(){this.panel()?.nativeElement.close();this.open.set(false);}
 reset(){this.large.set(false);this.contrast.set(false);this.links.set(false);this.motion.set(false);this.apply();}
 apply(){if(this.motion())this.doc.querySelectorAll('video').forEach(video=>video.pause());const values={large:this.large(),contrast:this.contrast(),links:this.links(),motion:this.motion()};for(const [key,value] of Object.entries(values))this.doc.documentElement.classList.toggle('so-'+key,value);try{localStorage.setItem('so-display',JSON.stringify(values));}catch{}}
}
