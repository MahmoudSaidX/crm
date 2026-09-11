import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { AppMenu } from './app-menu';
import { ShellMenuItem } from './shell-menu-item';

@Component({
  selector: 'sc-app-sidebar',
  imports: [AppMenu],
  template: `<div class="layout-sidebar">
    <nav [attr.aria-label]="navigationLabel()">
      <sc-app-menu [items]="items()" [navigationLabel]="navigationLabel()" />
    </nav>
  </div>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppSidebar {
  readonly items = input.required<readonly ShellMenuItem[]>();
  readonly navigationLabel = input.required<string>();
}
