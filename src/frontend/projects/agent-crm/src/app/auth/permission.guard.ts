import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthorizationService } from './authorization.service';

export const requirePermission =
  (code: string): CanActivateFn =>
  async () => {
    const authorization = inject(AuthorizationService);
    const router = inject(Router);
    await authorization.load();
    return authorization.state.has(code) ? true : router.createUrlTree(['/forbidden']);
  };
