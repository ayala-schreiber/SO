import { ScarfService } from './scarf';
import { of, throwError, Subject } from 'rxjs';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { CartService } from './cart';
import { Scarf } from './scarf';

describe('CartService', () => {
  const product: Scarf = { id: 1, name: 'Silk scarf', price: 120, imageUrl: '/silk.jpg', category: 'silk' };
  beforeEach(() => {
    localStorage.removeItem('so-cart');
    TestBed.configureTestingModule({providers:[{provide:ScarfService,useValue:{getScarves:()=>of([product]),checkAvailability:()=>of({available:true,message:null})}}]});
  });
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.restoreAllMocks();
    localStorage.removeItem('so-cart');
  });
  const cart = () => TestBed.inject(CartService);

  it('starts with an empty cart', () => {
    expect(cart().cartItems()).toEqual([]);
    expect(cart().itemCount()).toBe(0);
    expect(cart().totalPrice()).toBe(0);
  });
  it('merges repeated additions and totals different products', () => {
    cart().addToCart(product);
    cart().addToCart(product);
    cart().addToCart({ ...product, id: 2, price: 90 });
    expect(cart().cartItems().length).toBe(2);
    expect(cart().itemCount()).toBe(3);
    expect(cart().totalPrice()).toBe(330);
  });
  it('updates quantity and removes the line at zero', () => {
    cart().addToCart(product);
    cart().updateQuantity(1, 3);
    expect(cart().totalPrice()).toBe(360);
    cart().updateQuantity(1, 0);
    expect(cart().cartItems()).toEqual([]);
  });
  it('retains sold out colors and allows correcting an excessive quantity',()=>{
    cart().addToCart(product,5);
    vi.spyOn(TestBed.inject(ScarfService),'checkAvailability').mockImplementation((_id,q)=>of({available:q<=1,message:q<=1?null:'נותרה יחידה אחת במלאי'}));
    cart().refreshCatalog();expect(cart().itemCount()).toBe(5);expect(cart().canCheckout()).toBe(false);
    cart().updateQuantity(1,4);expect(cart().itemCount()).toBe(4);expect(cart().canCheckout()).toBe(false);
    cart().updateQuantity(1,1);expect(cart().canCheckout()).toBe(true);
    vi.spyOn(TestBed.inject(ScarfService),'checkAvailability').mockReturnValue(of({available:false,message:'אזל מהמלאי'}));
    cart().refreshCatalog();expect(cart().itemCount()).toBe(1);expect(cart().cartItems()[0].stockError).toBe('אזל מהמלאי');expect(cart().canCheckout()).toBe(false);
  });
  it('fails closed when availability cannot be loaded',()=>{
    cart().addToCart(product);vi.spyOn(TestBed.inject(ScarfService),'checkAvailability').mockReturnValue(throwError(()=>new Error('offline')));
    cart().refreshCatalog();expect(cart().itemCount()).toBe(1);expect(cart().catalogReady()).toBe(false);expect(cart().canCheckout()).toBe(false);
  });
  it('ignores an older refresh that completes after a newer one',()=>{
    cart().addToCart(product);const old=new Subject<Scarf[]>();const latest=new Subject<Scarf[]>();
    vi.spyOn(TestBed.inject(ScarfService),'getScarves').mockReturnValueOnce(old).mockReturnValueOnce(latest);
    cart().refreshCatalog();cart().refreshCatalog();latest.next([{...product,price:180}]);latest.complete();old.next([{...product,price:1}]);old.complete();
    expect(cart().totalPrice()).toBe(180);expect(cart().canCheckout()).toBe(true);
  });
  it('removes one product without changing the other', () => {
    cart().addToCart(product);
    cart().addToCart({ ...product, id: 2 });
    cart().removeFromCart(1);
    expect(cart().cartItems()).toEqual([{ product: { ...product, id: 2 }, quantity: 1 }]);
  });
  it('persists changes and restores them in a new service instance', () => {
    cart().addToCart(product);
    cart().updateQuantity(1, 2);
    TestBed.tick();
    expect(JSON.parse(localStorage.getItem('so-cart')!)).toEqual([{productId:1,quantity:2}]);
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({providers:[{provide:ScarfService,useValue:{getScarves:()=>of([product]),checkAvailability:()=>of({available:true,message:null})}}]});
    expect(cart().cartItems()).toEqual([{ product, quantity: 2 }]);
    expect(cart().totalPrice()).toBe(240);
  });
  it('persists clearing the cart', () => {
    cart().addToCart(product);
    TestBed.tick();
    cart().clearCart();
    TestBed.tick();
    expect(JSON.parse(localStorage.getItem('so-cart')!)).toEqual([]);
  });
  it.each(['broken json', 'null', '{}', '[{}]', JSON.stringify([{ product, quantity: -1 }]), JSON.stringify([{ product, quantity: 1.5 }]), JSON.stringify([{ product: { ...product, price: '120' }, quantity: 1 }])])('recovers safely from invalid saved data: %s', saved => {
    localStorage.setItem('so-cart', saved);
    expect(cart().cartItems()).toEqual([]);
    expect(cart().totalPrice()).toBe(0);
    cart().addToCart(product);
    expect(cart().totalPrice()).toBe(120);
  });
  it.each([NaN, Infinity, 1.5, Number.MAX_SAFE_INTEGER + 1])('ignores invalid quantity %s', quantity => {
    cart().addToCart(product);
    cart().updateQuantity(1, quantity);
    expect(cart().itemCount()).toBe(1);
  });
  it('works in memory when storage reads are blocked', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('blocked'); });
    cart().addToCart(product);
    expect(cart().totalPrice()).toBe(120);
  });
  it('works in memory when storage writes fail', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('quota exceeded'); });
    cart().addToCart(product);
    expect(() => TestBed.tick()).not.toThrow();
    expect(cart().totalPrice()).toBe(120);
  });

  it('checks the total requested quantity including the existing cart line', () => {
    const api = TestBed.inject(ScarfService);
    const check = vi.spyOn(api, 'checkAvailability').mockReturnValue(of({available:true,message:null}));
    cart().addToCart(product, 2);
    cart().addToCart(product, 3);
    expect(check).toHaveBeenLastCalledWith(product.id, 5);
    expect(cart().itemCount()).toBe(5);
  });
  it('keeps colors as separate cart lines', () => {
    cart().addToCart({...product, groupKey:'silk-collection', color:'כסף'});
    cart().addToCart({...product, id:2, groupKey:'silk-collection', color:'בורדו'});
    expect(cart().cartItems().length).toBe(2);
    expect(cart().cartItems().map(item=>item.product.color)).toEqual(['כסף','בורדו']);
  });
  it('does not change the cart when the server rejects the quantity', () => {
    cart().addToCart(product);
    vi.spyOn(TestBed.inject(ScarfService),'checkAvailability').mockReturnValue(of({available:false,message:'נותרה יחידה אחת במלאי'}));
    cart().addToCart(product);
    expect(cart().itemCount()).toBe(1);
    expect(cart().message()).toBe('נותרה יחידה אחת במלאי');
  });
  it('retains archived products, refreshes prices and blocks checkout until removal', () => {
    cart().addToCart(product);
    cart().addToCart({...product,id:2});
    TestBed.inject(ScarfService).getScarves = () => of([{...product,price:149}]);
    cart().refreshCatalog();
    expect(cart().cartItems().length).toBe(2);
    expect(cart().cartItems()[0].product.price).toBe(149);
    expect(cart().cartItems()[1].stockError).toBe('אזל מהמלאי');
    expect(cart().canCheckout()).toBe(false);
    cart().removeFromCart(2);expect(cart().canCheckout()).toBe(true);
  });
});
