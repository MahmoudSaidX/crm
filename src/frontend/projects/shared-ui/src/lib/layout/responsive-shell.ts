import { ChangeDetectionStrategy, Component, input, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';

export interface ShellNavigationItem {
  readonly label: string;
  readonly icon: string;
  readonly routerLink: string;
  readonly exact?: boolean;
}

@Component({
  selector: 'sc-responsive-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, ButtonModule, DrawerModule],
  templateUrl: './responsive-shell.html',
  styleUrl: './responsive-shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResponsiveShell {
  readonly title = input.required<string>();
  readonly logoUrl = input<string | null>(null);
  readonly menuLabel = input.required<string>();
  readonly closeMenuLabel = input.required<string>();
  readonly navigationLabel = input.required<string>();
  readonly navigationItems = input.required<readonly ShellNavigationItem[]>();
  protected readonly mobileNavigationVisible = signal(false);

  protected closeMobileNavigation(): void {
    this.mobileNavigationVisible.set(false);
  }
}
