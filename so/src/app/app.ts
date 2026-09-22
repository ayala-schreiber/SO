import {StoreContactService} from './services/store-contact';
import { Component, signal, inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { RouterOutlet, RouterLink } from '@angular/router';
import { AccessibilityControls } from './pages/accessibility-controls';
import { HeaderComponent } from './header/header';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, HeaderComponent, AccessibilityControls],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
 contact=inject(StoreContactService);
  private document=inject(DOCUMENT);
  skip(event:Event){event.preventDefault();this.document.getElementById('main-content')?.focus();}
  protected readonly title = signal('so');
}
