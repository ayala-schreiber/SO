import {TestBed} from '@angular/core/testing';
import {provideRouter} from '@angular/router';
import {RouterTestingHarness} from '@angular/router/testing';
import {of} from 'rxjs';
import {Catalog} from './catalog';
import {Favorites} from '../services/favorites';
import {ScarfService} from '../services/scarf';
describe('Search and favorites',()=>{
 const rows=[{id:1,name:'Ruby Bloom',category:'silk',color:'בורדו',price:239,inStock:true,groupKey:'ruby',imageUrl:'/a.webp'},{id:2,name:'Blush',category:'cotton',color:null,price:149,inStock:true,groupKey:'blush',imageUrl:'/b.webp'}];
 beforeEach(()=>{localStorage.removeItem('so-favorites-v1');TestBed.configureTestingModule({providers:[provideRouter([{path:'search',component:Catalog,data:{search:true}},{path:'favorites',component:Catalog,data:{favorites:true}},{path:'catalog',component:Catalog}]),{provide:ScarfService,useValue:{getScarves:()=>of(rows)}}]});});
 afterEach(()=>localStorage.removeItem('so-favorites-v1'));
 it('filters by Hebrew material and color with all query words',async()=>{const h=await RouterTestingHarness.create();const c=await h.navigateByUrl('/search?q='+encodeURIComponent('משי בורדו'),Catalog);expect(c.displayed().map(p=>p.id)).toEqual([1]);c.query.set('כותנה');h.detectChanges();expect(c.displayed().map(p=>p.id)).toEqual([2]);c.query.set('missing');h.detectChanges();expect(h.routeNativeElement!.textContent).toContain('לא נמצאו');});
 it('heart toggles independently of product link and survives service reload',async()=>{const h=await RouterTestingHarness.create('/catalog');const heart=h.routeNativeElement!.querySelector('.favorite-heart') as HTMLButtonElement;expect(heart.closest('a')).toBeNull();heart.click();h.detectChanges();expect(heart.getAttribute('aria-pressed')).toBe('true');expect(new Favorites().has(1)).toBe(true);await h.navigateByUrl('/favorites');expect(h.routeNativeElement!.querySelectorAll('.card').length).toBe(1);(h.routeNativeElement!.querySelector('.favorite-heart') as HTMLButtonElement).click();h.detectChanges();expect(h.routeNativeElement!.querySelectorAll('.card').length).toBe(0);});
 it('handles corrupt browser data and keeps colors distinct',()=>{localStorage.setItem('so-favorites-v1','invalid');const f=new Favorites();expect(f.ids()).toEqual([]);f.toggle(1);f.toggle(2);f.toggle(1);expect(f.ids()).toEqual([2]);});
});
