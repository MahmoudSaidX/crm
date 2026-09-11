import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ProgressSpinnerModule } from 'primeng/progressspinner';

export type PanelState = 'loading' | 'empty' | 'error';

/** One presentation for the loading, empty and error states of every screen. */
@Component({
  selector: 'sc-state-panel',
  imports: [ProgressSpinnerModule],
  template: `
    <div class="sc-state-panel" [attr.role]="state() === 'error' ? 'alert' : 'status'">
      @if (state() === 'loading') {
        <p-progress-spinner styleClass="sc-state-spinner" strokeWidth="4" />
      } @else {
        <i class="sc-state-icon {{ resolvedIcon() }}" aria-hidden="true"></i>
      }
      <p class="sc-state-message">{{ message() }}</p>
      <ng-content select="[actions]" />
    </div>
  `,
  styles: `
    .sc-state-panel {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.75rem;
      padding-block: 3rem;
      text-align: center;
      color: var(--text-color-secondary);
    }

    .sc-state-icon {
      font-size: 2rem;
    }

    .sc-state-message {
      margin: 0;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatePanel {
  readonly state = input.required<PanelState>();
  readonly message = input.required<string>();
  readonly icon = input<string | null>(null);
  protected readonly resolvedIcon = computed(
    () => this.icon() ?? (this.state() === 'error' ? 'pi pi-exclamation-triangle' : 'pi pi-inbox'),
  );
}
