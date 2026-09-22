import { Component, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule, NgForm } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize, switchMap } from 'rxjs';
interface ManagedProduct {fabricDescription?:string|null;suitableFor?:string|null;opacity?:string|null;slip?:string|null;breathability?:string|null;stretch?:string|null;season?:string|null;bobo?:string|null;summerCollection:boolean;id:number;isActive:boolean;width?:number;length?:number;name:string;price:number;size:string;color:string|null;category:string;stockQuantity:number;version:number;originalPrice:number|null;onSale:boolean;regularPrice:number;salePrice:number|null;groupKey:string;imageUrl:string;imagesJson?:string|null;gallery:string[];}
interface ProductGroup {groupKey:string;name:string;colors:number;}
@Component({standalone:true,imports:[FormsModule,RouterLink],styleUrl:'./admin.css',template:`
 <section class="inventory" dir="rtl"><a routerLink="/admin">חזרה לניהול</a><h2>מוצרים ומלאי</h2>
 <p>כמויות המלאי מוצגות רק לך. הכמות הראשונית היא 5 יחידות לכל מטפחת או צבע; יש לעדכן לפי המלאי בפועל.</p>
 @if (message()) { <p role="status">{{message()}}</p> }
 <details class="inventory-row"><summary>הוספת מטפחת חדשה</summary>
 <form #createForm="ngForm" (ngSubmit)="create(createForm, imageInput)">
 <label>שם המטפחת <input name="newName" [(ngModel)]="draft.name" required maxlength="150"></label>
 <label>סוג בד <select name="newCategory" [(ngModel)]="draft.category" required><option value="cotton">כותנה</option><option value="silk">משי</option><option value="crepe-satin">קרפ סאטן</option></select></label>
 <fieldset><legend>היכן להציג את המטפחת?</legend><p>מופיעה אוטומטית בכל המטפחות ובסוג הבד שבחרת.</p><label class="sale-toggle"><input type="checkbox" name="newSummer" [(ngModel)]="draft.summerCollection"> קולקציית קיץ</label></fieldset><fieldset class="size-field"><legend>מידה בס״מ</legend><div class="size-inputs" dir="ltr"><input aria-label="רוחב המטפחת החדשה" name="newWidth" type="number" [(ngModel)]="newWidth" (ngModelChange)="setDraftSize()" required min="1" max="1000" step="0.1" placeholder="65"><span aria-hidden="true">×</span><input aria-label="אורך המטפחת החדשה" name="newLength" type="number" [(ngModel)]="newLength" (ngModelChange)="setDraftSize()" required min="1" max="1000" step="0.1" placeholder="65"></div></fieldset>
 <details><summary>פירוט ותכונות הבד (לא חובה)</summary>@for(field of contentFields;track field.key){<label>{{field.label}}<textarea [name]="'new'+field.key" [(ngModel)]="draft[field.key]" [maxlength]="field.max" rows="2"></textarea></label>}</details>
 <label>צבע (לא חובה) <input name="newColor" [(ngModel)]="draft.color" maxlength="50"></label>
 <fieldset><legend>האם זו מטפחת חדשה או צבע נוסף?</legend>
 <label>שיוך לדגם <select name="newGroup" [(ngModel)]="draft.groupKey"><option value="">דגם חדש ונפרד</option>@for(g of groups();track g.groupKey){<option [value]="g.groupKey">צבע נוסף ל־{{g.name}} ({{g.colors}} כבר בדגם)</option>}</select></label>
 <p>צבעים של אותו דגם מוצגים יחד בעמוד המוצר עם בורר צבעים. בחירה ב״דגם חדש״ יוצרת מטפחת עצמאית.</p></fieldset>
 <label>מחיר בש״ח <input type="number" name="newPrice" [(ngModel)]="draft.price" required min="0.01" max="1000000" step="0.01"></label>
 <label>מלאי פרטי <input type="number" name="newStock" [(ngModel)]="draft.stockQuantity" required min="0" max="1000000" step="1"></label>
 <label>תמונות המטפחת (עד 3) <input #imageInput type="file" multiple accept="image/jpeg,image/png,image/webp" (change)="chooseImage($event)"></label>
 <p>עד 3 תמונות JPG, PNG או WebP, עד 5 מגה־בייט לכל תמונה. הראשונה תשמש כתמונה הראשית. לאחר שמירה המטפחת תופיע בחנות. אפשר להגדיר לה מבצע ברשימה למטה.</p>
 <button type="submit" [disabled]="createForm.invalid || !newImage || busy()!==null">{{busy()===-2?'מוסיפים…':'הוספת מטפחת לחנות'}}</button>
 </form></details>
 <button (click)="load()" [disabled]="busy()!==null">רענון רשימה</button>
 @for (p of products(); track p.id) {
 @if (p.isActive !== false) {
 <form #form="ngForm" (ngSubmit)="save(p)" class="inventory-row">
 <h3>{{p.name}}{{p.color?' — '+p.color:''}}</h3>
 <label>שם <input [name]="'name'+p.id" [(ngModel)]="p.name" required maxlength="150"></label>
 <fieldset class="size-field"><legend>מידה בס״מ</legend><div class="size-inputs" dir="ltr"><input [attr.aria-label]="'רוחב '+p.name" [name]="'width'+p.id" type="number" [(ngModel)]="p.width" (ngModelChange)="p.size=p.width+'×'+p.length" required min="1" max="1000" step="0.1"><span aria-hidden="true">×</span><input [attr.aria-label]="'אורך '+p.name" [name]="'length'+p.id" type="number" [(ngModel)]="p.length" (ngModelChange)="p.size=p.width+'×'+p.length" required min="1" max="1000" step="0.1"></div></fieldset>
 <label>סוג בד <select [name]="'category'+p.id" [(ngModel)]="p.category" required><option value="cotton">כותנה</option><option value="silk">משי</option><option value="crepe-satin">קרפ סאטן</option></select></label>
 <details><summary>פירוט ותכונות הבד</summary>@for(field of contentFields;track field.key){<label>{{field.label}}<textarea [name]="field.key+p.id" [(ngModel)]="p[field.key]" [maxlength]="field.max" rows="2"></textarea></label>}</details>
 <label>צבע <input [name]="'color'+p.id" [(ngModel)]="p.color" maxlength="50" placeholder="למשל: בורדו"></label>
 <label>שיוך לדגם <select [name]="'group'+p.id" [(ngModel)]="p.groupKey">@for(g of groupsFor(p);track g.groupKey){<option [value]="g.groupKey">{{g.name}} ({{g.colors}} צבעים)</option>}</select></label>
 <fieldset class="product-images"><legend>תמונות המטפחת</legend>
 <div class="image-strip">@for(src of p.gallery;track src){<img [src]="src" [alt]="p.name+(p.color?' — '+p.color:'')" width="64" height="64" loading="lazy">}</div>
 <label>החלפת התמונות (עד 3) <input type="file" multiple accept="image/jpeg,image/png,image/webp" (change)="choosePending(p,$event)"></label>
 @if(pendingCount(p)){<button type="button" (click)="replaceImages(p)" [disabled]="busy()!==null">{{busy()===p.id?'מעלה…':'החלפה ל־'+pendingCount(p)+' תמונות'}}</button>}
 <p>ההעלאה מחליפה את כל התמונות הקיימות. הראשונה תהיה התמונה הראשית.</p></fieldset>
 <label>מחיר רגיל בש״ח <input type="number" [name]="'price'+p.id" [(ngModel)]="p.regularPrice" required min="0.01" max="1000000" step="0.01"></label>
 <label class="sale-toggle"><input type="checkbox" [name]="'onSale'+p.id" [(ngModel)]="p.onSale"> הצגה במבצעים</label>
 @if (p.onSale) { <label>מחיר מבצע בש״ח <input type="number" [name]="'salePrice'+p.id" [(ngModel)]="p.salePrice" required min="0.01" [max]="p.regularPrice-0.01" step="0.01"></label><p>לאחר שמירה, המטפחת תופיע בלשונית מבצעים. ההגדרה חלה על הצבע הזה בלבד.</p> }
 <label class="sale-toggle"><input type="checkbox" [name]="'summer'+p.id" [(ngModel)]="p.summerCollection"> קולקציית קיץ</label><label>מלאי פרטי <input type="number" [name]="'stock'+p.id" [(ngModel)]="p.stockQuantity" required min="0" max="1000000" step="1"></label>
 <button type="submit" [disabled]="form.invalid || busy()!==null">{{busy()===p.id?'שומר…':'שמירה'}}</button>
 <button type="button" class="danger-button" (click)="visibility(p,false)" [disabled]="busy()!==null">הסרה מהחנות</button>
 </form> } }
 <details class="inventory-row"><summary>מטפחות שהוסרו — אפשרות שחזור</summary><p>הסרה מסתירה את המטפחת מהלקוחות. אפשר להחזיר אותה לחנות בכל עת.</p>
 @for (p of products(); track p.id) { @if (p.isActive === false) { <div class="archive-row"><span>{{p.name}}{{p.color?' — '+p.color:''}}</span><button type="button" (click)="visibility(p,true)" [disabled]="busy()!==null">החזרה לחנות</button></div> } }</details>
 </section>`})
export class AdminProducts {
 contentFields=[{key:'fabricDescription',label:'תיאור הבד',max:2000},{key:'suitableFor',label:'למה המטפחת מתאימה',max:500},{key:'opacity',label:'אטימות',max:100},{key:'slip',label:'מידת החלקה',max:100},{key:'breathability',label:'אווריריות',max:100},{key:'stretch',label:'אלסטיות',max:100},{key:'season',label:'התאמה לעונה',max:100},{key:'bobo',label:'האם נדרש בובו',max:100}] as const;
 private http=inject(HttpClient);products=signal<ManagedProduct[]>([]);message=signal('');busy=signal<number|null>(null);
 draft={fabricDescription:'',suitableFor:'',opacity:'',slip:'',breathability:'',stretch:'',season:'',bobo:'',name:'',size:'',category:'cotton',summerCollection:false,color:'',groupKey:'',price:null as number|null,stockQuantity:5};
 newWidth:number|null=null;newLength:number|null=null;
 setDraftSize(){this.draft.size=this.newWidth&&this.newLength?this.newWidth+'×'+this.newLength:'';}
 newImage:File|null=null;newImages:File[]=[];
 visibility(p:ManagedProduct,isActive:boolean){if(this.busy()!==null)return;this.busy.set(p.id);this.message.set('');
 this.http.get<{token:string}>('/api/admin/csrf').pipe(switchMap(({token})=>this.http.put<ManagedProduct>('/api/admin/products/'+p.id+'/visibility',{isActive,version:p.version},{headers:{'X-CSRF-TOKEN':token}})),finalize(()=>this.busy.set(null))).subscribe({next:updated=>{this.products.update(rows=>rows.map(row=>row.id===p.id?this.toEditable(updated):row));this.message.set(isActive?'המטפחת הוחזרה לחנות.':'המטפחת הוסרה מהחנות. ניתן לשחזר אותה למטה.');},error:(e:HttpErrorResponse)=>this.message.set(e.status===409?'המוצר השתנה. רעננו לפני ניסיון נוסף.':e.status===401?'ההתחברות פגה. חזרו לכניסת המנהל.':'הפעולה לא הצליחה. נסו שוב.')});
 }

 chooseImage(event:Event){const input=event.target as HTMLInputElement;const files=Array.from(input.files??[]);this.newImage=null;this.newImages=[];if(files.length>3||files.some(file=>file.size===0||file.size>5*1024*1024||!['image/jpeg','image/png','image/webp'].includes(file.type))){this.message.set('בחרו עד 3 תמונות JPG, PNG או WebP, עד 5 מגה־בייט לכל אחת.');input.value='';return;}this.newImages=files;this.newImage=files[0]??null;this.message.set(files.length?'נבחרו '+files.length+' תמונות. הראשונה תהיה ראשית.':'');}
 create(form:NgForm, imageInput?:HTMLInputElement){if(this.busy()!==null || !this.newImage || form.invalid)return;
 if(!this.draft.name.trim() || !this.draft.size.trim() || !Number.isInteger(this.draft.stockQuantity)){this.message.set('בדקו שם, מידה ומלאי שלם.');return;}
 const body=new FormData();for(const [key,value] of Object.entries(this.draft))body.append(key,String(value??''));body.append('image',this.newImage);for(const file of this.newImages.slice(1))body.append('images',file);
 this.busy.set(-2);this.message.set('');
 this.http.get<{token:string}>('/api/admin/csrf').pipe(switchMap(({token})=>this.http.post<ManagedProduct>('/api/admin/products',body,{headers:{'X-CSRF-TOKEN':token}})),finalize(()=>this.busy.set(null))).subscribe({next:p=>{
 this.products.update(rows=>[this.toEditable(p),...rows]);this.newImage=null;this.newImages=[];if(imageInput)imageInput.value='';this.draft={fabricDescription:'',suitableFor:'',opacity:'',slip:'',breathability:'',stretch:'',season:'',bobo:'',name:'',size:'',category:'cotton',summerCollection:false,color:'',groupKey:'',price:null,stockQuantity:5};this.newWidth=null;this.newLength=null;form.resetForm(this.draft);this.loadGroups();this.message.set('המטפחת נוספה לחנות בהצלחה.');
 },error:(e:HttpErrorResponse)=>this.message.set(e.status===401?'ההתחברות פגה. חזרו לכניסת המנהל.':e.error?.message??'ההוספה לא הצליחה. בדקו את השדות והתמונה ונסו שוב.')});
 }
 private toEditable(p:ManagedProduct):ManagedProduct{const dimensions=p.size?.split(/[×xX]/).map(Number)??[];return {...p,width:dimensions[0],length:dimensions[1],gallery:this.gallery(p),regularPrice:p.onSale?(p.originalPrice??p.price):p.price,salePrice:p.onSale?p.price:null};}
 // השרת שומר את הגלריה כמחרוזת JSON; מוצר ישן ללא גלריה נופל חזרה לתמונה הראשית.
 private gallery(p:ManagedProduct):string[]{try{const parsed=JSON.parse(p.imagesJson||'[]');return Array.isArray(parsed)&&parsed.length?parsed.filter((s:unknown)=>typeof s==='string'):[p.imageUrl];}catch{return p.imageUrl?[p.imageUrl]:[];}}

 groups=signal<ProductGroup[]>([]);
 loadGroups(){this.http.get<ProductGroup[]>('/api/admin/products/groups').subscribe({next:rows=>this.groups.set(rows),error:()=>{}});}
 // הדגם הנוכחי חייב להופיע ברשימה גם כשהוא דגם יחיד, אחרת הבחירה תתאפס בטעות.
 groupsFor(p:ManagedProduct):ProductGroup[]{const rows=this.groups();return rows.some(g=>g.groupKey===p.groupKey)?rows:[{groupKey:p.groupKey,name:p.name,colors:1},...rows];}

 pendingImages:Record<number,File[]>={};
 pendingCount(p:ManagedProduct){return this.pendingImages[p.id]?.length??0;}
 choosePending(p:ManagedProduct,event:Event){const input=event.target as HTMLInputElement;const files=Array.from(input.files??[]);delete this.pendingImages[p.id];
  if(!files.length)return;
  if(files.length>3||files.some(file=>file.size===0||file.size>5*1024*1024||!['image/jpeg','image/png','image/webp'].includes(file.type))){this.message.set('בחרו עד 3 תמונות JPG, PNG או WebP, עד 5 מגה־בייט לכל אחת.');input.value='';return;}
  this.pendingImages[p.id]=files;this.message.set('');}
 replaceImages(p:ManagedProduct){const files=this.pendingImages[p.id];if(this.busy()!==null||!files?.length)return;
  this.busy.set(p.id);this.message.set('');const body=new FormData();body.append('version',String(p.version));for(const file of files)body.append('images',file);
  this.http.get<{token:string}>('/api/admin/csrf').pipe(switchMap(({token})=>this.http.put<ManagedProduct>('/api/admin/products/'+p.id+'/images',body,{headers:{'X-CSRF-TOKEN':token}})),finalize(()=>this.busy.set(null))).subscribe({next:updated=>{Object.assign(p,this.toEditable(updated));delete this.pendingImages[p.id];this.message.set('התמונות הוחלפו.');},error:(e:HttpErrorResponse)=>this.message.set(e.status===409?'המוצר השתנה. רעננו את הרשימה לפני החלפת תמונות.':e.status===401?'ההתחברות פגה. חזרו לכניסת המנהל.':e.error?.message??'ההחלפה לא הצליחה. נסו שוב.')});}

 constructor(){this.load();this.loadGroups();}
 load(){this.busy.set(-1);this.message.set('טוענים מוצרים…');this.http.get<ManagedProduct[]>('/api/admin/products?includeArchived=true').pipe(finalize(()=>this.busy.set(null))).subscribe({next:rows=>{this.products.set(rows.map(p=>this.toEditable(p)));this.message.set('');},error:(e:HttpErrorResponse)=>this.message.set(e.status===401?'ההתחברות פגה. חזרו לכניסת המנהל.':'לא ניתן לטעון מוצרים כרגע.')});}
 save(p:ManagedProduct){if(this.busy()!==null)return;if(!Number.isInteger(p.stockQuantity)){this.message.set('המלאי חייב להיות מספר שלם.');return;}
 this.busy.set(p.id);this.message.set('');const body={category:p.category,color:p.color,groupKey:p.groupKey,fabricDescription:p.fabricDescription,suitableFor:p.suitableFor,opacity:p.opacity,slip:p.slip,breathability:p.breathability,stretch:p.stretch,season:p.season,bobo:p.bobo,summerCollection:p.summerCollection,name:p.name,size:p.size,price:p.regularPrice,onSale:p.onSale,salePrice:p.onSale?p.salePrice:null,stockQuantity:p.stockQuantity,version:p.version};
 this.http.get<{token:string}>('/api/admin/csrf').pipe(switchMap(({token})=>this.http.put<ManagedProduct>('/api/admin/products/'+p.id,body,{headers:{'X-CSRF-TOKEN':token}})),finalize(()=>this.busy.set(null))).subscribe({next:updated=>{Object.assign(p,this.toEditable(updated));this.message.set('השינויים נשמרו.');},error:(e:HttpErrorResponse)=>this.message.set(e.status===409?'המוצר השתנה. רעננו את הרשימה לפני שמירה נוספת.':e.status===401?'ההתחברות פגה. חזרו לכניסת המנהל.':'השמירה לא הצליחה. בדקו את הערכים ונסו שוב.')});
 }
}
