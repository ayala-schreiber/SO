import { Component, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { CartService } from '../services/cart';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './header.html',
  styleUrls: ['./header.css']
})
export class HeaderComponent {
  isMenuOpen = false;

  constructor(public cartService: CartService) {}

  @ViewChild('menuToggle') menuToggle?:ElementRef<HTMLButtonElement>;
  @ViewChild('menuPanel') menuPanel?:ElementRef<HTMLElement>;
  toggleMenu() {this.isMenuOpen=!this.isMenuOpen;setTimeout(()=>{if(this.isMenuOpen)this.menuPanel?.nativeElement.querySelector('button')?.focus();else this.menuToggle?.nativeElement.focus();});}
  menuKey(event:KeyboardEvent){if(event.key==='Escape'){event.preventDefault();this.toggleMenu();return;}if(event.key==='Tab'){const items=Array.from(this.menuPanel!.nativeElement.querySelectorAll<HTMLElement>('button,a[href]'));const first=items[0],last=items[items.length-1];if(event.shiftKey&&event.target===first){event.preventDefault();last.focus();}else if(!event.shiftKey&&event.target===last){event.preventDefault();first.focus();}}}
}