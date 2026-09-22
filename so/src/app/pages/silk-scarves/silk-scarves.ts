import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ScarfService, Scarf } from '../../services/scarf';
import { CartService } from '../../services/cart';

@Component({
  selector: 'app-silk-scarves',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './silk-scarves.html',
  styleUrls: ['./silk-scarves.css']
})
export class SilkScarvesComponent implements OnInit {
  silkScarves = signal<Scarf[]>([]);

  constructor(private scarfService: ScarfService, public cartService: CartService) { }

  ngOnInit() {
    this.scarfService.getScarves().subscribe(data => {
      this.silkScarves.set(data.filter(item => item.category === 'silk'));
    });
  }

  addToCart(scarf: Scarf) {
    this.cartService.addToCart(scarf);
  }
}
