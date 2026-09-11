import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { ShellMenuItem } from './shell-menu-item';

/**
 * Sakai-style sidebar menu. Root entries render as section headings; their children
 * render as routed links. Adapted from PrimeNG Sakai (MIT, PrimeTek) 20.0.0.
 */
@Component({
  selector: 'sc-app-menu',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './app-menu.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppMenu {
  readonly items = input.required<readonly ShellMenuItem[]>();
  readonly navigationLabel = input.required<string>();
}
