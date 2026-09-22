import {Injectable,signal} from '@angular/core';
@Injectable({providedIn:'root'})
export class Favorites {
 private key='so-favorites-v1';ids=signal<number[]>(this.load());message=signal('');
 private load():number[]{try{const ids=JSON.parse(localStorage.getItem(this.key)||'[]');return Array.isArray(ids)?[...new Set<number>(ids.filter(x=>Number.isSafeInteger(x)&&x>0))].slice(0,1000):[];}catch{return [];}}
 has(id:number){return this.ids().includes(id);}
 toggle(id:number){if(!Number.isSafeInteger(id)||id<1)return;const selected=this.has(id);this.ids.update(ids=>selected?ids.filter(x=>x!==id):[...ids,id].slice(-1000));try{localStorage.setItem(this.key,JSON.stringify(this.ids()));this.message.set(selected?'הוסר מהמועדפים':'נוסף למועדפים');}catch{this.message.set('המועדפים זמינים עד סגירת העמוד; הדפדפן לא מאפשר שמירה.');}}
}
