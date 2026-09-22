import { Component, Input, OnInit, inject, signal, computed } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Favorites } from '../services/favorites';
import { ScarfService, Scarf } from '../services/scarf';

@Component({selector:'app-catalog',standalone:true,imports:[RouterLink,FormsModule],styleUrl:'./catalog.css',template:`
  <section class="catalog" dir="rtl">
    @if(featured){<h2>מהקטלוג שלנו</h2>}@else{<h1>{{title}}</h1>}
    @if (searchEnabled) {<label class="search-label">חיפוש לפי שם, צבע או בד<input type="search" [ngModel]="query()" (ngModelChange)="query.set($event)" placeholder="למשל: בורדו, משי או Ruby" aria-label="חיפוש מטפחות"></label>}
    @if(favoritesOnly){<p>המועדפים נשמרים בדפדפן הזה, ללא צורך בהרשמה.</p>}
    @if(favorites.message()){<p role="status">{{favorites.message()}}</p>}
    @if (loading()) { <p role="status">טוענים את המטפחות…</p> }
    @if (error()) { <p role="alert">{{error()}}</p><button (click)="load()">נסו שוב</button> }
    <div class="grid">
      @for (p of displayed(); track p.id) {
        <div class="product-tile"><button class="favorite-heart" type="button" [class.liked]="favorites.has(p.id)" [attr.aria-pressed]="favorites.has(p.id)" [attr.aria-label]="(favorites.has(p.id)?'הסרה מהמועדפים: ':'הוספה למועדפים: ')+p.name+(p.color?' '+p.color:'')" (click)="favorites.toggle(p.id)">{{favorites.has(p.id)?'♥':'♡'}}</button><a class="card" [routerLink]="['/products',p.groupKey]" [queryParams]="p.color ? {color:p.color} : null">
          <img [src]="p.imageUrl" [alt]="p.name" loading="lazy">
          @if(featured){<h3>{{p.name}}</h3>}@else{<h2>{{p.name}}</h2>}<p>{{material(p.category)}}</p>
          <div class="card-prices">@if (p.onSale && p.originalPrice) { <span class="sale-badge">מבצע</span><del class="old-price" dir="ltr">{{p.originalPrice}}&nbsp;₪</del> }
          <strong class="current-price" dir="ltr">{{p.price}}&nbsp;₪</strong></div>
          @if (p.color) { <p>{{p.color}}</p> }
          @if (!p.inStock) { <p>אזל במלאי</p> }
        </a></div>
      }
    </div>
    @if (!loading() && !error() && displayed().length === 0) { <p>{{favoritesOnly?'עוד לא נשמרו מטפחות זמינות. לחצו על הלב בקטלוג.':searchEnabled?'לא נמצאו מטפחות לחיפוש הזה. נסו שם, צבע או בד אחר.':'אין כרגע מטפחות בקטגוריה זו.'}}</p> }
  </section>`})
export class Catalog implements OnInit {
  @Input() featured=false;
  private api=inject(ScarfService);private route=inject(ActivatedRoute);
  favorites=inject(Favorites);query=signal('');searchEnabled=false;favoritesOnly=false;
  displayed=computed(()=>{const words=this.query().trim().toLocaleLowerCase().split(/\s+/).filter(Boolean);return this.products().filter(p=>(!this.favoritesOnly||this.favorites.has(p.id))&&words.every(w=>(p.name+' '+(p.color||'')+' '+this.material(p.category)).toLocaleLowerCase().includes(w)));});
  products=signal<Scarf[]>([]);loading=signal(true);error=signal('');title='כל המטפחות';
  material(c:string){return c==='cotton'?'כותנה':c==='silk'?'משי':'קרפ סאטן';}
  ngOnInit(){this.route.queryParamMap.subscribe(p=>this.query.set(p.get('q')||''));this.route.data.subscribe(()=>this.load());}
  load(){this.loading.set(true);this.error.set('');const data=this.route.snapshot.data;this.title=data['title']||'כל המטפחות';this.searchEnabled=!!data['search'];this.favoritesOnly=!!data['favorites'];
    this.api.getScarves().subscribe({next:rows=>{
      rows=rows.filter(p=>!data['category']||p.category===data['category']).filter(p=>!data['sale']||p.onSale).filter(p=>!data['summer']||p.summerCollection);
      const items=rows;this.products.set(this.featured?items.slice(0,4):items);this.loading.set(false);
    },error:()=>{this.error.set('לא ניתן לטעון את הקטלוג כרגע.');this.loading.set(false);}});
  }
}
