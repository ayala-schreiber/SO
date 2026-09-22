import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ScarfService, Scarf } from '../../services/scarf';
import { CartService } from '../../services/cart';

@Component({
  selector: 'app-summer-collection',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './summer-collection.html',
  styleUrls: ['./summer-collection.css']
})
export class SummerCollection implements OnInit {
  summerScarves = signal<Scarf[]>([]);

  constructor(private scarfService: ScarfService, public cartService: CartService) { }

  ngOnInit() {
    this.scarfService.getScarves().subscribe(data => {
      this.summerScarves.set(data.filter(item => item.summerCollection === true));
    });
  }

  addToCart(scarf: Scarf) {
    this.cartService.addToCart(scarf);
  }
}