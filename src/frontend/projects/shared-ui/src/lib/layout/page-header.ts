import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Page identity block: title, optional subtitle and a projected `[actions]` slot. */
@Component({
  selector: 'sc-page-header',
  template: `
    <header class="sc-page-header">
      <div class="sc-page-header-text">
        <h1>{{ title() }}</h1>
        @if (subtitle()) {
          <p class="sc-page-header-subtitle">{{ subtitle() }}</p>
        }
      </div>
      <div class="sc-page-header-actions"><ng-content select="[actions]" /></div>
    </header>
  `,
  styles: `
    .sc-page-header {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-start;
      justify-content: space-between;
      gap: 1rem;
      margin-block-end: 1.5rem;
    }

    .sc-page-header-text {
      min-inline-size: 0;
    }

    h1 {
      margin: 0;
    }

    .sc-page-header-subtitle {
      margin-block: 0.25rem 0;
      color: var(--text-color-secondary);
    }

    .sc-page-header-actions {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem;
      margin-inline-start: auto;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PageHeader {
  readonly title = input.required<string>();
  readonly subtitle = input<string | null>(null);
}
