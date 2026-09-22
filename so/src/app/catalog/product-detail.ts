import {ProductZoom} from './product-zoom';
import { combineLatest } from 'rxjs';
import { Component, inject, signal, viewChild, ElementRef } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Scarf, ScarfService } from '../services/scarf';
import { Favorites } from '../services/favorites';
import { CartService } from '../services/cart';

@Component({standalone:true,imports:[RouterLink,FormsModule,ProductZoom],styleUrls:['./catalog.css','./product-details.css'],template:`
 <section class="catalog" dir="rtl">
 @if (error()) { <p role="alert">{{error()}}</p> }
 @if (loading()) { <p role="status">טוענים את המטפחת…</p> }
 @if (selected(); as p) {
 <div class="detail"><div class="product-tile"><div #gallery class="photo-gallery" tabindex="0" role="region" aria-label="תמונות המטפחת — אפשר לגלול למטה" (scroll)="galleryScrolled()">@for(src of photos(p);track $index){<button class="photo-trigger" type="button" [attr.aria-label]="'הגדלת תמונה '+($index+1)+' של '+p.name" (click)="zoom.open($index,$event.currentTarget)"><img class="product-photo" [src]="src" [alt]="p.name+(p.color?' — '+p.color:'')+' · תמונה '+($index+1)" [loading]="$index===0?'eager':'lazy'"></button>}</div><p class="zoom-hint">לחצו על התמונה להגדלה</p><app-product-zoom #zoom [images]="photos(p)" [name]="p.name+(p.color?' — '+p.color:'')" />@if(photos(p).length>1){<div class="gallery-nav" aria-label="בחירת תמונה">@for(src of photos(p);track $index){<button type="button" [class.active]="photoIndex()===$index" [attr.aria-label]="'הצגת תמונה '+($index+1)" [attr.aria-pressed]="photoIndex()===$index" (click)="showPhoto($index)"><img [src]="src" alt=""></button>}<span class="gallery-count" dir="ltr">{{photoIndex()+1}} / {{photos(p).length}}</span></div>}<button type="button" class="favorite-heart" [class.liked]="favorites.has(p.id)" [attr.aria-pressed]="favorites.has(p.id)" [attr.aria-label]="favorites.has(p.id)?'הסרה מהמועדפים':'הוספה למועדפים'" (click)="favorites.toggle(p.id)">{{favorites.has(p.id)?'♥':'♡'}}</button></div>
 <div><a routerLink="/catalog">לכל המטפחות</a><h1>{{p.name}}</h1>
 <p>{{material(p.category)}} · {{p.size}} ס״מ</p><div class="product-price">@if (p.onSale && p.originalPrice) { <span class="sale-badge">מבצע</span><del class="old-price" dir="ltr">{{p.originalPrice}}&nbsp;₪</del> }<strong class="current-price" dir="ltr">{{p.price}}&nbsp;₪</strong></div>
 @if (variants().length > 1) { <fieldset><legend>בחירת צבע</legend>
 @for (variant of variants(); track variant.id) { <button type="button" class="color" [class.selected]="p.id===variant.id" [attr.aria-pressed]="p.id===variant.id" (click)="choose(variant)">{{variant.color}}</button> }
 </fieldset> }
 @if (p.color) { <p>צבע: {{p.color}}</p> }
 @if (!p.inStock) { <p>אזל במלאי</p> }
 <label for="quantity" class="quantity-label">כמות</label>
 <div class="quantity-picker" dir="ltr">
 <button type="button" aria-label="הפחתת כמות" (click)="changeQuantity(-1)" [disabled]="quantity<=1 || cart.busy()">−</button>
 <input id="quantity" type="number" min="1" max="1000000" step="1" inputmode="numeric" [(ngModel)]="quantity" [disabled]="cart.busy()">
 <button type="button" aria-label="הגדלת כמות" (click)="changeQuantity(1)" [disabled]="quantity>=1000000 || cart.busy()">+</button>
 </div>
 <button (click)="add(p)" [disabled]="!p.inStock || cart.busy()">{{cart.busy()?'בודקים זמינות…':'הוספה לסל'}}</button>
 @if (cart.message()) { <p role="status">{{cart.message()}}</p> }
 <a routerLink="/cart">לעגלת הקניות</a>
 @if(p.fabricDescription||p.suitableFor||traits(p).length){<section class="fabric-details" aria-label="פרטי הבד והתאמה"><h2>להכיר את המטפחת</h2>@if(p.fabricDescription){<p>{{p.fabricDescription}}</p>}@if(p.suitableFor){<h2>למה היא מתאימה?</h2><p>{{p.suitableFor}}</p>}@if(traits(p).length){<dl class="fabric-traits">@for(trait of traits(p);track trait.label){<div><dt>{{trait.label}}</dt><dd>{{trait.value}}</dd></div>}</dl>}</section>}
 </div></div>
 }
 </section>`})
export class ProductDetail {
 traits(p:Scarf){return [{label:'אטימות',value:p.opacity},{label:'מידת החלקה',value:p.slip},{label:'אווריריות',value:p.breathability},{label:'אלסטיות',value:p.stretch},{label:'עונה',value:p.season},{label:'בובו',value:p.bobo}].filter(t=>t.value?.trim());}

 gallery=viewChild<ElementRef<HTMLElement>>('gallery');photoIndex=signal(0);
 photos(p:Scarf){return p.images?.length?p.images:[p.imageUrl];}
 galleryScrolled(){const el=this.gallery()?.nativeElement;if(el?.clientHeight)this.photoIndex.set(Math.round(el.scrollTop/el.clientHeight));}
 showPhoto(index:number){const el=this.gallery()?.nativeElement;if(!el)return;el.scrollTo({top:index*el.clientHeight,behavior:matchMedia('(prefers-reduced-motion: reduce)').matches||document.documentElement.classList.contains('so-motion')?'instant':'smooth'});}

 favorites=inject(Favorites);
 private api=inject(ScarfService);private route=inject(ActivatedRoute);cart=inject(CartService);
 variants=signal<Scarf[]>([]);selected=signal<Scarf|null>(null);error=signal('');loading=signal(true);quantity=1;
 constructor(){combineLatest([this.route.paramMap,this.route.queryParamMap]).subscribe(([params,query])=>{this.loading.set(true);this.selected.set(null);this.photoIndex.set(0);this.error.set('');
  this.api.getScarves().subscribe({next:rows=>{const variants=rows.filter(p=>p.groupKey===params.get('group'));this.variants.set(variants);this.selected.set(variants.find(p=>p.color===query.get('color'))||variants[0]||null);this.quantity=1;this.loading.set(false);if(!variants.length)this.error.set('המטפחת אינה זמינה בקטלוג.');},error:()=>{this.loading.set(false);this.error.set('לא ניתן לטעון את המטפחת כרגע.');}});
 });}
 material(c:string){return c==='cotton'?'כותנה':c==='silk'?'משי':'קרפ סאטן';}
 choose(p:Scarf){this.selected.set(p);this.photoIndex.set(0);this.gallery()?.nativeElement.scrollTo({top:0,behavior:'instant'});this.quantity=1;this.cart.message.set('');}
 changeQuantity(delta:number){const current=Number.isSafeInteger(this.quantity)?this.quantity:1;this.quantity=Math.max(1,Math.min(1000000,current+delta));}
 add(p:Scarf){this.cart.addToCart(p,this.quantity);}
}
