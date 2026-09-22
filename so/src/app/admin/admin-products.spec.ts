import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { NgForm } from '@angular/forms';
import { AdminProducts } from './admin-products';
describe('Create scarf',()=>{
 let http:HttpTestingController;
 beforeEach(()=>{TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting(),provideRouter([])]});http=TestBed.inject(HttpTestingController);});
 afterEach(()=>http.verify());
 it('sends image and fields with CSRF and inserts saved product once',()=>{
  const fixture=TestBed.createComponent(AdminProducts);const page=fixture.componentInstance;
  http.expectOne('/api/admin/products?includeArchived=true').flush([]);
  http.expectOne('/api/admin/products/groups').flush([]);
  fixture.detectChanges();expect(fixture.nativeElement.textContent).toContain('הוספת מטפחת חדשה');
  page.draft={...page.draft,name:'New scarf',size:'65×65',category:'cotton',summerCollection:false,color:'Blue',fabricDescription:'תיאור הבד',suitableFor:'לשימוש יומיומי',price:149,stockQuantity:5};
  page.newImage=new File(['image'],'scarf.png',{type:'image/png'});
  const resetForm=vi.fn();const form={invalid:false,resetForm} as unknown as NgForm;
  page.create(form);page.create(form);
  http.expectOne('/api/admin/csrf').flush({token:'test-csrf'});
  const request=http.expectOne('/api/admin/products');expect(request.request.method).toBe('POST');
  expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('test-csrf');
  expect(request.request.body.get('fabricDescription')).toBe('תיאור הבד');expect(request.request.body.get('suitableFor')).toBe('לשימוש יומיומי');expect(request.request.body.get('stockQuantity')).toBe('5');expect(request.request.body.get('image').name).toBe('scarf.png');
  request.flush({id:999,name:'New scarf',size:'65×65',category:'cotton',summerCollection:false,price:149,stockQuantity:5,onSale:false,groupKey:'scarf-new',imageUrl:'/products/uploads/a.png'});
  // הוספה מרעננת את רשימת הדגמים, כדי שהצבע הבא יוכל להצטרף לדגם שזה עתה נוצר.
  http.expectOne('/api/admin/products/groups').flush([{groupKey:'scarf-new',name:'New scarf',colors:1}]);
  expect(page.groups().length).toBe(1);
  expect(page.products().length).toBe(1);expect(page.products()[0].regularPrice).toBe(149);expect(page.newImage).toBeNull();expect(resetForm).toHaveBeenCalled();expect(page.busy()).toBeNull();
 });
 it('keeps the draft and image when saving fails',()=>{
  const page=TestBed.createComponent(AdminProducts).componentInstance;http.expectOne('/api/admin/products?includeArchived=true').flush([]);http.expectOne('/api/admin/products/groups').flush([]);
  page.draft={...page.draft,name:'Keep me',size:'65×65',category:'cotton',summerCollection:false,color:'',price:149,stockQuantity:5};
  page.newImage=new File(['x'],'scarf.png',{type:'image/png'});
  page.create({invalid:false} as NgForm);http.expectOne('/api/admin/csrf').flush({token:'t'});
  http.expectOne('/api/admin/products').flush({message:'Invalid image'},{status:400,statusText:'Bad Request'});
  expect(page.draft.name).toBe('Keep me');expect(page.newImage).not.toBeNull();expect(page.message()).toBe('Invalid image');expect(page.busy()).toBeNull();
 });
});

describe('Edit scarf colour, model grouping and photos',()=>{
 let http:HttpTestingController;
 const row=(over:Record<string,unknown>={})=>({id:7,name:'ליבנה',size:'70×70',category:'silk',color:'בורדו',groupKey:'livne',
  imageUrl:'/products/uploads/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-0.webp',imagesJson:null,price:239,originalPrice:null,onSale:false,
  summerCollection:false,stockQuantity:5,version:3,isActive:true,...over});
 const start=(rows:unknown[]=[row()],groups:unknown[]=[{groupKey:'livne',name:'ליבנה',colors:1}])=>{
  const page=TestBed.createComponent(AdminProducts).componentInstance;
  http.expectOne('/api/admin/products?includeArchived=true').flush(rows);
  http.expectOne('/api/admin/products/groups').flush(groups);
  return page;};
 beforeEach(()=>{TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting(),provideRouter([])]});http=TestBed.inject(HttpTestingController);});
 afterEach(()=>http.verify());

 it('saves colour and model so a scarf can join an existing colour group',()=>{
  const page=start();const p=page.products()[0];
  p.color='תכלת';p.groupKey='silk-collection';
  page.save(p);
  http.expectOne('/api/admin/csrf').flush({token:'t'});
  const request=http.expectOne('/api/admin/products/7');
  expect(request.request.method).toBe('PUT');
  expect(request.request.body.color).toBe('תכלת');
  expect(request.request.body.groupKey).toBe('silk-collection');
  expect(request.request.body.version).toBe(3);
  request.flush({...row(),color:'תכלת',groupKey:'silk-collection',version:4});
  expect(page.products()[0].groupKey).toBe('silk-collection');
 });

 it('keeps the scarf own model in the list even when the server did not return it',()=>{
  const page=start([row()],[{groupKey:'other',name:'אחר',colors:2}]);
  const options=page.groupsFor(page.products()[0]).map(g=>g.groupKey);
  expect(options).toContain('livne');
  expect(options).toContain('other');
 });

 it('falls back to the main photo when the gallery is empty, and shows all three when it is not',()=>{
  const page=start([row(),row({id:8,imagesJson:'["/a.webp","/b.webp","/c.webp"]'})]);
  expect(page.products()[0].gallery).toEqual(['/products/uploads/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-0.webp']);
  expect(page.products()[1].gallery.length).toBe(3);
 });

 it('replaces every photo in one request carrying the version and a fresh token',()=>{
  const page=start();const p=page.products()[0];
  const input={files:[new File(['a'],'1.png',{type:'image/png'}),new File(['b'],'2.png',{type:'image/png'})],value:'x'} as unknown as HTMLInputElement;
  page.choosePending(p,{target:input} as unknown as Event);
  expect(page.pendingCount(p)).toBe(2);
  page.replaceImages(p);page.replaceImages(p);
  http.expectOne('/api/admin/csrf').flush({token:'fresh'});
  const request=http.expectOne('/api/admin/products/7/images');
  expect(request.request.method).toBe('PUT');
  expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('fresh');
  expect(request.request.body.get('version')).toBe('3');
  expect(request.request.body.getAll('images').length).toBe(2);
  request.flush({...row(),version:4,imagesJson:'["/x-0.png","/x-1.png"]'});
  expect(page.pendingCount(p)).toBe(0);
  expect(page.products()[0].gallery).toEqual(['/x-0.png','/x-1.png']);
  expect(page.busy()).toBeNull();
 });

 it('refuses a fourth photo and an oversized one without calling the server',()=>{
  const page=start();const p=page.products()[0];
  const png=(name:string)=>new File(['a'],name,{type:'image/png'});
  page.choosePending(p,{target:{files:[png('1'),png('2'),png('3'),png('4')],value:'x'} as unknown as HTMLInputElement} as unknown as Event);
  expect(page.pendingCount(p)).toBe(0);
  const big=new File([new Uint8Array(5*1024*1024+1)],'big.png',{type:'image/png'});
  page.choosePending(p,{target:{files:[big],value:'x'} as unknown as HTMLInputElement} as unknown as Event);
  expect(page.pendingCount(p)).toBe(0);
  page.replaceImages(p);
 });
});
