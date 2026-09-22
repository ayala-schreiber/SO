import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { Catalog } from './catalog';
import { ProductDetail } from './product-detail';
import { ScarfService } from '../services/scarf';
import { CartService } from '../services/cart';

describe('Catalog presentation',()=>{
 const products=[{id:1,name:'Silk Collection',category:'silk',size:'70×70',groupKey:'silk-collection',color:'כסף',price:239,inStock:true,imageUrl:'/silver.webp',images:['/silver.webp','/detail.webp','/back.webp']},
 {id:2,name:'Silk Collection',category:'silk',size:'70×70',groupKey:'silk-collection',color:'בורדו',price:199,originalPrice:239,onSale:true,inStock:true,imageUrl:'/burgundy.webp'}];
 const add=vi.fn();
 beforeEach(()=>{add.mockReset();TestBed.configureTestingModule({providers:[provideRouter([
 {path:'catalog',component:Catalog},{path:'sale',component:Catalog,data:{sale:true,title:'מבצעים'}},{path:'products/:group',component:ProductDetail}]),
 {provide:ScarfService,useValue:{getScarves:()=>of(products)}},{provide:CartService,useValue:{busy:signal(false),message:signal(''),addToCart:add}}]});});
 it('shows every color and hides dimensions in browsing cards',async()=>{
  const h=await RouterTestingHarness.create('/catalog');
  const el=h.routeNativeElement!;
  expect(el.querySelectorAll('.card').length).toBe(2);
  expect(el.textContent).not.toContain('70×70');
  expect(el.querySelectorAll('.card')[1].getAttribute('href')).toContain('color=');
 });
 it('shows only discounted items in sale and displays the regular price',async()=>{
  const h=await RouterTestingHarness.create('/sale');
  expect(h.routeNativeElement!.querySelectorAll('.card').length).toBe(1);
  expect(h.routeNativeElement!.querySelector('del')!.textContent).toContain('239');
 });
 it('opens the clicked color, keeps dimensions in detail, supports stepper and typed amounts',async()=>{
  const h=await RouterTestingHarness.create();
  const component=await h.navigateByUrl('/products/silk-collection?color='+encodeURIComponent('בורדו'),ProductDetail);
  expect(component.selected()!.color).toBe('בורדו');
  expect(h.routeNativeElement!.textContent).toContain('70×70');
  (h.routeNativeElement!.querySelector('[aria-label="הגדלת כמות"]') as HTMLButtonElement).click();h.detectChanges();
  expect(component.quantity).toBe(2);
  const input=h.routeNativeElement!.querySelector('#quantity') as HTMLInputElement;
  input.value='4';input.dispatchEvent(new Event('input'));h.detectChanges();
  expect(component.quantity).toBe(4);
  component.add(component.selected()!);expect(add).toHaveBeenCalledWith(products[1],4);
  component.quantity=1;component.changeQuantity(-1);expect(component.quantity).toBe(1);
 });
 it('renders three gallery photos with accessible thumbnail selection',async()=>{const h=await RouterTestingHarness.create('/products/silk-collection');expect(h.routeNativeElement!.querySelectorAll('.photo-gallery img').length).toBe(3);expect(h.routeNativeElement!.querySelectorAll('.gallery-nav button').length).toBe(3);expect(h.routeNativeElement!.querySelector('.gallery-nav button')!.getAttribute('aria-pressed')).toBe('true');});

});
