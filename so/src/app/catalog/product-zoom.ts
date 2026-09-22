import {Component,ElementRef,viewChild,input,signal,inject,DestroyRef} from '@angular/core';

@Component({selector:'app-product-zoom',standalone:true,styleUrl:'./product-zoom.css',template:`
<dialog #dialog aria-labelledby="zoom-title" (cancel)="close()" (close)="closed()">
 <header><h2 id="zoom-title">{{name()}}</h2><button type="button" autofocus aria-label="סגירת התמונה המוגדלת" (click)="close()">×</button></header>
 <div class="zoom-toolbar" aria-label="כלי הגדלת תמונה">
  <button type="button" (click)="changeScale(-.5)" [disabled]="scale()<=1" aria-label="הקטנת התמונה">−</button>
  <button type="button" (click)="reset()" aria-label="התאמת התמונה למסך">{{scale()*100}}%</button>
  <button type="button" (click)="changeScale(.5)" [disabled]="scale()>=3" aria-label="הגדלת התמונה">+</button>
  @if(images().length>1){<button type="button" (click)="move(-1)" aria-label="התמונה הקודמת">→</button><span dir="ltr" aria-live="polite">{{index()+1}} / {{images().length}}</span><button type="button" (click)="move(1)" aria-label="התמונה הבאה">←</button>}
 </div>
 <div #viewport class="zoom-viewport" tabindex="0" role="region" aria-label="תמונה מוגדלת — לחצו להגדלה, גררו או השתמשו בחצים לתנועה" [class.enlarged]="scale()>1" [class.dragging]="dragging()" (click)="toggleZoom($event)" (pointerdown)="startDrag($event)" (pointermove)="drag($event)" (pointerup)="endDrag($event)" (pointercancel)="endDrag($event)" (lostpointercapture)="endDrag($event)" (keydown)="onKey($event)">
 @if(opened()){<div class="zoom-canvas" [style.width.%]="scale()*100" [style.height.%]="scale()*100"><img [src]="images()[index()]" [alt]="name()+' · תמונה '+(index()+1)" decoding="async" draggable="false"></div>}
 </div>
 <p>לחיצה על התמונה מגדילה או מקטינה אותה. לתנועה בתמונה מוגדלת, גררו עם העכבר או החליקו באצבע.</p>
</dialog>`})
export class ProductZoom {
 images=input.required<string[]>();name=input.required<string>();
 dialog=viewChild<ElementRef<HTMLDialogElement>>('dialog');viewport=viewChild<ElementRef<HTMLElement>>('viewport');
 index=signal(0);scale=signal(1);opened=signal(false);private trigger:HTMLElement|null=null;private previousOverflow:string|null=null;
 dragging=signal(false);private pointer:{id:number;x:number;y:number;left:number;top:number}|null=null;private moved=false;private frame=0;
 constructor(){inject(DestroyRef).onDestroy(()=>{cancelAnimationFrame(this.frame);this.restore();});}
 open(index:number,trigger:EventTarget|null){this.index.set(index);this.scale.set(1);this.trigger=trigger instanceof HTMLElement?trigger:null;this.opened.set(true);if(this.previousOverflow===null){this.previousOverflow=document.body.style.overflow;document.body.style.overflow='hidden';}this.dialog()?.nativeElement.showModal();this.viewport()?.nativeElement.scrollTo(0,0);}
 close(){this.dialog()?.nativeElement.close();this.closed();}
 closed(){cancelAnimationFrame(this.frame);this.pointer=null;this.dragging.set(false);this.opened.set(false);this.restore();this.trigger?.focus();}
 private restore(){if(this.previousOverflow!==null){document.body.style.overflow=this.previousOverflow;this.previousOverflow=null;}}
 reset(){cancelAnimationFrame(this.frame);this.pointer=null;this.dragging.set(false);this.scale.set(1);this.viewport()?.nativeElement.scrollTo(0,0);}
 changeScale(delta:number,point?:{x:number;y:number}){
 const el=this.viewport()?.nativeElement;if(!el)return;
 const before=this.scale(),after=Math.max(1,Math.min(3,before+delta));if(before===after)return;
 const x=point?.x??el.clientWidth/2,y=point?.y??el.clientHeight/2;
 const left=(el.scrollLeft+x)/before*after-x,top=(el.scrollTop+y)/before*after-y;
 this.scale.set(after);cancelAnimationFrame(this.frame);this.frame=requestAnimationFrame(()=>el.scrollTo({left:after===1?0:left,top:after===1?0:top,behavior:'instant'}));
 }
 toggleZoom(event:MouseEvent){if(this.moved){this.moved=false;return;}const el=this.viewport()?.nativeElement;if(!el)return;const r=el.getBoundingClientRect();this.changeScale(this.scale()===1?1:-this.scale()+1,{x:event.clientX-r.left,y:event.clientY-r.top});}
 startDrag(event:PointerEvent){this.moved=false;if(event.pointerType==='touch'||event.button!==0||this.scale()<=1)return;const el=this.viewport()?.nativeElement;if(!el)return;this.pointer={id:event.pointerId,x:event.clientX,y:event.clientY,left:el.scrollLeft,top:el.scrollTop};el.setPointerCapture(event.pointerId);}
 drag(event:PointerEvent){const p=this.pointer,el=this.viewport()?.nativeElement;if(!p||!el||p.id!==event.pointerId)return;const dx=event.clientX-p.x,dy=event.clientY-p.y;if(!this.moved&&Math.hypot(dx,dy)<5)return;this.moved=true;this.dragging.set(true);el.scrollTo({left:p.left-dx,top:p.top-dy,behavior:'instant'});event.preventDefault();}
 endDrag(event:PointerEvent){if(this.pointer?.id!==event.pointerId)return;this.pointer=null;this.dragging.set(false);const el=this.viewport()?.nativeElement;if(el?.hasPointerCapture(event.pointerId))el.releasePointerCapture(event.pointerId);}
 onKey(event:KeyboardEvent){if(event.key==='Enter'||event.key===' '){event.preventDefault();this.changeScale(this.scale()===1?1:1-this.scale());return;}const directions:Record<string,[number,number]>={ArrowLeft:[-80,0],ArrowRight:[80,0],ArrowUp:[0,-80],ArrowDown:[0,80]};const direction=directions[event.key];if(!direction)return;event.preventDefault();if(this.scale()>1)this.viewport()?.nativeElement.scrollBy({left:direction[0],top:direction[1],behavior:'instant'});else if(event.key==='ArrowLeft')this.move(1);else if(event.key==='ArrowRight')this.move(-1);}
 move(delta:number){this.index.update(n=>(n+delta+this.images().length)%this.images().length);this.reset();}
}
