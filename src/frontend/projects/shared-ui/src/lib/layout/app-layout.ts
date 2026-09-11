import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  input,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AppFooter } from './app-footer';
import { AppSidebar } from './app-sidebar';
import { AppTopbar } from './app-topbar';
import { LayoutService } from './layout.service';
import { ShellMenuItem } from './shell-menu-item';

/**
 * The Squad CRM application shell: Sakai's `layout-wrapper` composition of topbar,
 * sidebar, scrollable content area, footer and mobile mask.
 *
 * Adapted from PrimeNG Sakai (MIT, PrimeTek) 20.0.0, `app.layout.ts`.
 */
@Component({
  selector: 'sc-app-layout',
  imports: [AppTopbar, AppSidebar, AppFooter, RouterOutlet],
  templateUrl: './app-layout.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppLayout {
  readonly title = input.required<string>();
  readonly logoUrl = input<string | null>(null);
  readonly menuLabel = input.required<string>();
  readonly actionsLabel = input.required<string>();
  readonly darkModeLabel = input.required<string>();
  readonly navigationLabel = input.required<string>();
  readonly navigationItems = input.required<readonly ShellMenuItem[]>();
  readonly closeMenuLabel = input.required<string>();
  readonly footerText = input.required<string>();

  protected readonly layout = inject(LayoutService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly staticMenuDesktopInactive = computed(
    () => !this.layout.isOverlay() && this.layout.layoutState().staticMenuDesktopInactive,
  );
  protected readonly overlayMenuActive = computed(
    () => this.layout.layoutState().overlayMenuActive,
  );
  protected readonly mobileMenuActive = computed(
    () => this.layout.layoutState().staticMenuMobileActive,
  );

  constructor() {
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => this.layout.hideMenu());
  }
}
