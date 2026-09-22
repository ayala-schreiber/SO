import {StoreContactService} from '../../services/store-contact';
import { Catalog } from '../../catalog/catalog';
import {Component,inject,signal,viewChild,ElementRef,AfterViewInit} from '@angular/core';
@Component({selector:'app-home',standalone:true,imports:[Catalog],templateUrl:'./home.html',styleUrls:['./home.css']})
export class HomeComponent implements AfterViewInit{
 contact=inject(StoreContactService);
 chatOpen=signal(false);playing=signal(false);
 video=viewChild<ElementRef<HTMLVideoElement>>('heroVideo');chatToggle=viewChild<ElementRef<HTMLButtonElement>>('chatToggle');
 ngAfterViewInit(){if(typeof matchMedia==='function'&&!matchMedia('(prefers-reduced-motion: reduce)').matches&&!document.documentElement.classList.contains('so-motion'))this.video()?.nativeElement.play().catch(()=>{});}
 toggleChat(){this.chatOpen.update(open=>!open);}
 closeChat(){this.chatOpen.set(false);this.chatToggle()?.nativeElement.focus();}
 toggleVideo(){const video=this.video()?.nativeElement;if(!video)return;if(video.paused)video.play().catch(()=>{});else video.pause();}
}
