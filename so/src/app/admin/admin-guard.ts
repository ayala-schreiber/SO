import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AdminAuth } from './admin-auth';

export const adminGuard: CanActivateFn = () => {
  const router = inject(Router);
  return inject(AdminAuth).session().pipe(map(state => state.setupRequired ? router.createUrlTree(['/admin/security']) : true), catchError(() => of(router.createUrlTree(['/admin/login']))));
};

export const adminSecurityGuard: CanActivateFn = () => {
  const router = inject(Router);
  return inject(AdminAuth).session().pipe(map(() => true), catchError(() => of(router.createUrlTree(['/admin/login']))));
};
