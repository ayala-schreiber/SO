import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ScarfService, Scarf } from '../../services/scarf';
import { CartService } from '../../services/cart';

@Component({
  selector: 'app-cotton-scarves',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './cotton-scarves.html',
  styleUrls: ['./cotton-scarves.css']
})
export class CottonScarves implements OnInit {
  cottonScarves = signal<Scarf[]>([]);

  constructor(private scarfService: ScarfService, public cartService: CartService) { }

  ngOnInit() {
    this.scarfService.getScarves().subscribe(data => {
      this.cottonScarves.set(data.filter(item => item.category === 'cotton'));
    });
  }

  addToCart(scarf: Scarf) {
    this.cartService.addToCart(scarf);
  }
}