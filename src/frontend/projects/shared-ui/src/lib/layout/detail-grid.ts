import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Term/description grid for detail screens. Wraps a `<dl>` of `<dt>`/`<dd>` pairs and
 * collapses to a single column on narrow viewports.
 */
@Component({
  selector: 'sc-detail-grid',
  template: `<dl class="sc-detail-grid"><ng-content /></dl>`,
  styles: `
    .sc-detail-grid {
      display: grid;
      grid-template-columns: minmax(8rem, max-content) minmax(0, 1fr);
      gap: 0.5rem 1.5rem;
      margin: 0;
    }

    ::ng-deep .sc-detail-grid dt {
      color: var(--text-color-secondary);
      font-weight: 600;
    }

    ::ng-deep .sc-detail-grid dd {
      margin: 0;
      min-inline-size: 0;
      overflow-wrap: anywhere;
    }

    @media (max-width: 48rem) {
      .sc-detail-grid {
        grid-template-columns: minmax(0, 1fr);
        gap: 0.25rem;
      }

      ::ng-deep .sc-detail-grid dd {
        margin-block-end: 0.75rem;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DetailGrid {}
