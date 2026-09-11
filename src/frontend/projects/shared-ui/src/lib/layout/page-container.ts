import { ChangeDetectionStrategy, Component } from '@angular/core';

/** The single source of page padding and max width for every application screen. */
@Component({
  selector: 'sc-page-container',
  template: `<ng-content />`,
  styles: `
    :host {
      display: block;
      inline-size: 100%;
      max-inline-size: 90rem;
      margin-inline: auto;
      padding-block: 1.5rem;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PageContainer {}
