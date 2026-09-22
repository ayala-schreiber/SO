import { Injectable, signal, computed, effect } from '@angular/core';
import { Scarf, ScarfService } from './scarf';
import { inject } from '@angular/core';
import { finalize, forkJoin, of, switchMap, map } from 'rxjs';

export interface CartItem {
  product: Scarf;
  quantity: number;
  stockError?: string;
  catalogMissing?: boolean;
}

@Injectable({ providedIn: 'root' })
export class CartService {
  private api = inject(ScarfService);
  busy = signal(false);
  message = signal('');
  private readonly storageKey = 'so-cart';
  private items = signal<CartItem[]>(this.loadCart());

  catalogReady=signal(this.items().length===0);
  cartItems = this.items.asReadonly();
  hasMissingProducts=computed(()=>this.items().some(i=>i.catalogMissing));
  canCheckout=computed(()=>this.catalogReady()&&!this.busy()&&this.items().length>0&&this.items().every(i=>!i.stockError));
  private refreshVersion=0;

  itemCount = computed(() =>
    this.items().reduce((sum, item) => sum + item.quantity, 0)
  );

  totalPrice = computed(() =>
    this.items().reduce((sum, item) => sum + item.product.price * item.quantity, 0)
  );

  constructor() {
    if(this.items().length)this.refreshCatalog();
    effect(() => {
      const cartAsText = JSON.stringify(this.items().map(item=>({productId:item.product.id,quantity:item.quantity})));
      try {
        localStorage.setItem(this.storageKey, cartAsText);
      } catch {
        // Keep the cart usable in memory when browser storage is unavailable.
      }
    });
  }

  addToCart(product: Scarf, amount = 1) {
    if (this.busy()) return;
    if (!Number.isSafeInteger(amount) || amount < 1) { this.message.set('יש לבחור כמות שלמה וחיובית.'); return; }
    const existing = this.items().find(item => item.product.id === product.id);
    this.setQuantityChecked(product, (existing?.quantity ?? 0) + amount);
  }

  private setQuantityChecked(product: Scarf, quantity: number, allowInvalidUpdate=false) {
    if (!Number.isSafeInteger(quantity) || quantity > 1000000) { this.message.set('הכמות המבוקשת אינה תקינה.'); return; }
    this.busy.set(true); this.message.set('');
    this.api.checkAvailability(product.id, quantity).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: result => {
        if (!result.available) {
          const stockError=result.message||'הכמות אינה זמינה. יש לעדכן את הסל.';
          if(allowInvalidUpdate)this.items.update(items=>items.map(item=>item.product.id===product.id?{product,quantity,stockError}:item));
          this.message.set(stockError);return;
        }
        const current = this.items();
        this.items.set(current.some(item => item.product.id === product.id)
          ? current.map(item => item.product.id === product.id ? {product, quantity} : item)
          : [...current, {product, quantity}]);
        this.message.set('הסל עודכן.');
      }, error: () => this.message.set('לא ניתן לבדוק זמינות כרגע. הסל לא שונה; נסו שוב.')
    });
  }

  refreshCatalog() {
    const version=++this.refreshVersion;
    this.catalogReady.set(false);this.busy.set(true);this.message.set('');
    this.api.getScarves().pipe(switchMap(products=>{
      const current=this.items();
      if(!current.length)return of([] as CartItem[]);
      return forkJoin(current.map(item=>{
        const product=products.find(p=>p.id===item.product.id);
        if(!product)return of({...item,product:{...item.product,name:item.product.name==='טוענים פרטי מוצר…'?'מטפחת שאינה זמינה עוד':item.product.name,inStock:false},stockError:'אזל מהמלאי',catalogMissing:true});
        return this.api.checkAvailability(product.id,item.quantity).pipe(map(result=>result.available
          ? {product,quantity:item.quantity}
          : {product,quantity:item.quantity,stockError:result.message||'אזל מהמלאי'}));
      }));
    }),finalize(()=>{if(version===this.refreshVersion)this.busy.set(false);})).subscribe({
      next:items=>{if(version!==this.refreshVersion)return;this.items.set(items);this.catalogReady.set(true);},
      error:()=>{if(version===this.refreshVersion)this.message.set('לא ניתן לבדוק זמינות כרגע. נסו לטעון שוב לפני המשך להזמנה.');}
    });
  }

  removeFromCart(productId: number) {
    if (this.busy()) return;
    this.items.set(this.items().filter(item => item.product.id !== productId));
  }

  updateQuantity(productId: number, quantity: number) {
    if (this.busy() || !Number.isSafeInteger(quantity)) return;
    if (quantity <= 0) { this.removeFromCart(productId); return; }
    const product = this.items().find(item => item.product.id === productId)?.product;
    if (product) this.setQuantityChecked(product, quantity, true);
  }

  clearCart() {
    if (this.busy()) return;
    this.items.set([]);
  }

  private loadCart(): CartItem[] {
    try {
      const savedCart = localStorage.getItem(this.storageKey);
      if (!savedCart) return [];
      const raw:unknown=JSON.parse(savedCart);
      const parsed:unknown=Array.isArray(raw)?raw.map(item=>item&&typeof item==='object'&&Number.isSafeInteger(item.productId)&&item.productId>0?{product:{id:item.productId,name:'טוענים פרטי מוצר…',price:0,category:'',imageUrl:''},quantity:item.quantity}:item):raw;
      if (!Array.isArray(parsed) || !parsed.every(item => this.isCartItem(item))) return [];
      if (new Set(parsed.map(item => item.product.id)).size !== parsed.length) return [];
      return parsed;
    } catch {
      return [];
    }
  }

  private isCartItem(value: unknown): value is CartItem {
    if (!value || typeof value !== 'object') return false;
    const item = value as Partial<CartItem>;
    const product = item.product;
    return Number.isSafeInteger(item.quantity) && item.quantity! > 0 &&
      !!product && typeof product === 'object' &&
      Number.isSafeInteger(product.id) && product.id > 0 &&
      typeof product.name === 'string' && typeof product.category === 'string' &&
      typeof product.imageUrl === 'string' &&
      typeof product.price === 'number' && Number.isFinite(product.price) && product.price >= 0;
  }
}
