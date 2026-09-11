import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'sc-app-footer',
  template: `<div class="layout-footer">
    <span>{{ text() }}</span>
  </div>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppFooter {
  readonly text = input.required<string>();
}
