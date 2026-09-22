import {TestBed} from '@angular/core/testing';
import {provideHttpClient} from '@angular/common/http';
import {provideHttpClientTesting} from '@angular/common/http/testing';
import {CartPage} from './cart';
import {CartService} from '../../services/cart';
describe('Configured shipping',()=>{
 let subtotal=350;
 function page(){TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting(),{provide:CartService,useValue:{totalPrice:()=>subtotal}}]});const p=TestBed.createComponent(CartPage).componentInstance;p.shipping.set({deliveryFee:50,freeDeliveryAbove:350,pickupEnabled:true,pickupAddress:'Address'});return p;}
 it('grants free shipping at the threshold and charges below it',()=>{const p=page();subtotal=349.99;expect(p.shippingFee()).toBe(50);subtotal=350;expect(p.shippingFee()).toBe(0);});
 it('uses changed settings and free pickup',()=>{const p=page();subtotal=200;p.shipping.set({deliveryFee:35,freeDeliveryAbove:400,pickupEnabled:true,pickupAddress:'New address'});expect(p.shippingFee()).toBe(35);p.pickup.set(true);expect(p.shippingFee()).toBe(0);});
 it('does not grant free pickup when pickup disabled',()=>{const p=page();subtotal=200;p.pickup.set(true);p.shipping.set({deliveryFee:50,freeDeliveryAbove:350,pickupEnabled:false,pickupAddress:''});expect(p.shippingFee()).toBe(50);});
});
