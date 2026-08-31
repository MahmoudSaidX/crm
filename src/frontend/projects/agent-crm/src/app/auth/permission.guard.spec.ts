import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { AuthorizationService } from './authorization.service';
import { AuthorizationState } from './authorization.state';
import { requirePermission } from './permission.guard';

describe('requirePermission', () => {
  it('allows a granted capability and redirects a denied capability', async () => {
    const state = new AuthorizationState();
    state.set(['roles.view']);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthorizationService, useValue: { state, load: async () => undefined } },
      ],
    });

    const allowed = await TestBed.runInInjectionContext(() =>
      requirePermission('roles.view')({} as never, {} as never),
    );
    const denied = await TestBed.runInInjectionContext(() =>
      requirePermission('roles.manage')({} as never, {} as never),
    );

    expect(allowed).toBeTrue();
    expect(denied instanceof UrlTree).toBeTrue();
    expect(TestBed.inject(Router).serializeUrl(denied as UrlTree)).toBe('/forbidden');
  });
});
