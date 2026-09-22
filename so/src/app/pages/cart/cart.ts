import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { CartService } from '../../services/cart';
import { ShippingSettings, deliveryFee } from '../../services/shipping';

@Component({
  selector: 'app-cart',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './cart.html',
  styleUrls: ['./cart.css']
})
export class CartPage {
  private http=inject(HttpClient);
  shipping=signal<ShippingSettings|null>(null);
  shippingError=signal(false);pickup=signal(false);
  shippingFee(){return deliveryFee(this.shipping(),this.cartService.totalPrice(),this.pickup());}
  constructor(public cartService: CartService) {}
  ngOnInit() { this.cartService.refreshCatalog();this.http.get<ShippingSettings>('/api/store/shipping').subscribe({next:s=>this.shipping.set(s),error:()=>this.shippingError.set(true)}); }

  increase(productId: number, currentQty: number) {
    this.cartService.updateQuantity(productId, currentQty + 1);
  }

  decrease(productId: number, currentQty: number) {
    this.cartService.updateQuantity(productId, currentQty - 1);
  }

  remove(productId: number) {
    this.cartService.removeFromCart(productId);
  }
}