import { adminGuard, adminSecurityGuard } from './admin/admin-guard';
import { Routes } from '@angular/router';
import { HomeComponent } from './pages/home/home';
import { SilkScarvesComponent } from './pages/silk-scarves/silk-scarves';
import { CottonScarves } from './pages/cotton-scarves/cotton-scarves';
import { SummerCollection } from './pages/summer-collection/summer-collection';
import { OurStory } from './pages/our-story/our-story';
import { Sale } from './pages/sale/sale';
import { CartPage } from './pages/cart/cart';

export const routes: Routes = [
 {path:'admin/security',title:'אבטחת החשבון | SO',canActivate:[adminSecurityGuard],loadComponent:()=>import('./admin/admin-security').then(m=>m.AdminSecurityPage)},
 {path:'verify-email',title:"אימות מייל | SO",loadComponent:()=>import('./pages/verify-email').then(m=>m.VerifyEmail)},
 {path:'admin/reports',title:"ניהול החנות | SO",canActivate:[adminGuard],loadComponent:()=>import('./admin/sales-report').then(m=>m.SalesReport)},
 {path:'forgot-password',title:"איפוס סיסמה | SO",loadComponent:()=>import('./pages/password-reset').then(m=>m.PasswordReset)},
 {path:'reset-password',title:"בחירת סיסמה | SO",data:{reset:true},loadComponent:()=>import('./pages/password-reset').then(m=>m.PasswordReset)},
 {path:'admin/coupons',title:"ניהול החנות | SO",canActivate:[adminGuard],loadComponent:()=>import('./admin/admin-coupons').then(m=>m.AdminCoupons)},
 {path:'accessibility',title:"הצהרת נגישות | SO",data:{kind:'accessibility',title:'הצהרת נגישות'},loadComponent:()=>import('./pages/legal').then(m=>m.LegalPage)},
 {path:'privacy',title:"פרטיות | SO",data:{kind:'privacy',title:'פרטיות ומידע אישי'},loadComponent:()=>import('./pages/legal').then(m=>m.LegalPage)},
 {path:'terms',title:"תנאי שימוש | SO",data:{kind:'terms',title:'תנאי שימוש ומידע על החנות'},loadComponent:()=>import('./pages/legal').then(m=>m.LegalPage)},
 {path:'search',title:"חיפוש | SO",data:{search:true,title:'חיפוש מטפחות'},loadComponent:()=>import('./catalog/catalog').then(m=>m.Catalog)},
 {path:'favorites',title:"מועדפים | SO",data:{favorites:true,title:'המועדפים שלי'},loadComponent:()=>import('./catalog/catalog').then(m=>m.Catalog)},
 {path:'account',title:"החשבון שלי | SO",loadComponent:()=>import('./pages/account').then(m=>m.Account)},
 {path:'checkout',title:"הזמנה ותשלום | SO",loadComponent:()=>import('./pages/checkout').then(m=>m.Checkout)},
 {path:'orders/:id',title:"פרטי הזמנה | SO",loadComponent:()=>import('./pages/order-view').then(m=>m.OrderView)},
 {path:'admin/orders',title:"ניהול החנות | SO",canActivate:[adminGuard],loadComponent:()=>import('./admin/admin-commerce').then(m=>m.AdminCommerce)},
 {path:'admin/customers',title:"ניהול החנות | SO",canActivate:[adminGuard],data:{customers:true},loadComponent:()=>import('./admin/admin-commerce').then(m=>m.AdminCommerce)},
  {path:'admin/settings',title:"ניהול החנות | SO",canActivate:[adminGuard],loadComponent:()=>import('./admin/admin-settings').then(m=>m.AdminSettings)},
  { path: 'admin/login',title:"ניהול החנות | SO", loadComponent: () => import('./admin/admin-login').then(m => m.AdminLogin) },
  { path: 'admin/products',title:"ניהול החנות | SO", canActivate: [adminGuard], loadComponent: () => import('./admin/admin-products').then(m => m.AdminProducts) },
  { path: 'admin',title:"ניהול החנות | SO", canActivate: [adminGuard], loadComponent: () => import('./admin/admin-home').then(m => m.AdminHome) },
  { path: '',title:"SO — מטפחות | SO", component: HomeComponent },
  { path: 'catalog',title:"כל המטפחות | SO", loadComponent: () => import('./catalog/catalog').then(m => m.Catalog) },
  { path: 'products/:group',title:"פרטי מטפחת | SO", loadComponent: () => import('./catalog/product-detail').then(m => m.ProductDetail) },
  { path: 'silk-scarves',title:"מטפחות משי | SO", data: {category:'silk',title:'מטפחות משי'}, loadComponent: () => import('./catalog/catalog').then(m => m.Catalog) },
  { path: 'cotton-scarves',title:"מטפחות כותנה | SO", data: {category:'cotton',title:'מטפחות כותנה'}, loadComponent: () => import('./catalog/catalog').then(m => m.Catalog) },
  { path: 'crepe-satin',title:"מטפחות קרפ סאטן | SO", data: {category:'crepe-satin',title:'מטפחות קרפ סאטן'}, loadComponent: () => import('./catalog/catalog').then(m => m.Catalog) },
  { path: 'summer-collection',title:"קולקציית קיץ | SO", data: {summer:true,title:'קולקציית קיץ'}, loadComponent: () => import('./catalog/catalog').then(m => m.Catalog) },
  { path: 'sale',title:"מבצעים | SO", data: {sale:true,title:'מבצעים'}, loadComponent: () => import('./catalog/catalog').then(m => m.Catalog) },
  { path: 'our-story',title:"הסיפור שלנו | SO", component: OurStory }, { path: 'cart',title:"סל הקניות | SO", component: CartPage }
,{path:'**',title:'העמוד לא נמצא | SO',loadComponent:()=>import('./pages/not-found').then(m=>m.NotFound)}
];