import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LayoutService } from './layout.service';

/**
 * Sakai-style fixed topbar: menu trigger, brand, dark-mode toggle and a projected
 * `[shell-actions]` slot for application-specific actions.
 */
@Component({
  selector: 'sc-app-topbar',
  imports: [RouterLink],
  templateUrl: './app-topbar.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppTopbar {
  readonly title = input.required<string>();
  readonly logoUrl = input<string | null>(null);
  readonly menuLabel = input.required<string>();
  readonly actionsLabel = input.required<string>();
  readonly darkModeLabel = input.required<string>();
  protected readonly layout = inject(LayoutService);
  protected readonly actionsExpanded = signal(false);

  protected toggleActions(): void {
    this.actionsExpanded.update((expanded) => !expanded);
  }
}
