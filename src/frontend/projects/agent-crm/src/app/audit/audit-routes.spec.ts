import { TestBed } from '@angular/core/testing';
import { CanActivateFn, provideRouter, Router, Routes, UrlTree } from '@angular/router';
import { routes } from '../app.routes';
import { AuthorizationService } from '../auth/authorization.service';
import { AuthorizationState } from '../auth/authorization.state';

/**
 * Route-level proof (Test Plan item 7, CRM-114) that '/audit' and
 * '/audit/:id' are gated by the 'audit.view' permission — same pattern as
 * `permission.guard.spec.ts`, but exercised against the actual route
 * definitions in app.routes.ts rather than a standalone guard invocation.
 */
describe('audit routes permission gating', () => {
  function shellChildren(): Routes {
    const shellRoute = routes.find((route) => route.path === '' && route.children);
    return shellRoute?.children ?? [];
  }

  function configureWithPermissions(codes: readonly string[]): void {
    const state = new AuthorizationState();
    state.set(codes);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthorizationService, useValue: { state, load: async () => undefined } },
      ],
    });
  }

  it('defines /audit and /audit/:id with a canActivate guard', () => {
    const children = shellChildren();
    const auditList = children.find((route) => route.path === 'audit');
    const auditDetail = children.find((route) => route.path === 'audit/:id');

    expect(auditList?.canActivate?.length).toBe(1);
    expect(auditDetail?.canActivate?.length).toBe(1);
  });

  it('allows /audit when audit.view is granted', async () => {
    configureWithPermissions(['audit.view']);
    const guard = shellChildren().find((route) => route.path === 'audit')!
      .canActivate![0] as CanActivateFn;

    const result = await TestBed.runInInjectionContext(() => guard({} as never, {} as never));

    expect(result).toBeTrue();
  });

  it('redirects /audit to /forbidden when audit.view is not granted', async () => {
    configureWithPermissions(['roles.view']);
    const guard = shellChildren().find((route) => route.path === 'audit')!
      .canActivate![0] as CanActivateFn;

    const result = await TestBed.runInInjectionContext(() => guard({} as never, {} as never));

    expect(result instanceof UrlTree).toBeTrue();
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/forbidden');
  });

  it('redirects /audit/:id to /forbidden when audit.view is not granted', async () => {
    configureWithPermissions([]);
    const guard = shellChildren().find((route) => route.path === 'audit/:id')!
      .canActivate![0] as CanActivateFn;

    const result = await TestBed.runInInjectionContext(() => guard({} as never, {} as never));

    expect(result instanceof UrlTree).toBeTrue();
  });
});
