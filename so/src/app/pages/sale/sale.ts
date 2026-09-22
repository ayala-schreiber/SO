import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ScarfService, Scarf } from '../../services/scarf';
import { CartService } from '../../services/cart';

@Component({
  selector: 'app-sale',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './sale.html',
  styleUrls: ['./sale.css']
})
export class Sale implements OnInit {
  saleScarves = signal<Scarf[]>([]);

  constructor(private scarfService: ScarfService, public cartService: CartService) { }

  ngOnInit() {
    this.scarfService.getScarves().subscribe(data => {
      this.saleScarves.set(data.filter(item => item.onSale === true));
    });
  }

  getDiscountPercent(scarf: Scarf): number {
    if (!scarf.originalPrice) return 0;
    return Math.round((1 - scarf.price / scarf.originalPrice) * 100);
  }

  addToCart(scarf: Scarf) {
    this.cartService.addToCart(scarf);
  }
}