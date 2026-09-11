import { ComponentFixture, TestBed } from '@angular/core/testing';
import { StatePanel } from './state-panel';

describe('StatePanel', () => {
  async function render(
    state: 'loading' | 'empty' | 'error',
  ): Promise<ComponentFixture<StatePanel>> {
    await TestBed.configureTestingModule({ imports: [StatePanel] }).compileComponents();
    const fixture = TestBed.createComponent(StatePanel);
    fixture.componentRef.setInput('state', state);
    fixture.componentRef.setInput('message', 'Nothing here');
    fixture.detectChanges();
    return fixture;
  }

  it('renders a spinner while loading', async () => {
    const fixture = await render('loading');
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('p-progress-spinner')).not.toBeNull();
    expect(element.textContent).toContain('Nothing here');
  });

  it('renders an alert for the error state', async () => {
    const fixture = await render('error');
    const panel = (fixture.nativeElement as HTMLElement).querySelector('.sc-state-panel');

    expect(panel?.getAttribute('role')).toBe('alert');
    expect(panel?.querySelector('.pi-exclamation-triangle')).not.toBeNull();
  });

  it('renders a status region for the empty state', async () => {
    const fixture = await render('empty');
    const panel = (fixture.nativeElement as HTMLElement).querySelector('.sc-state-panel');

    expect(panel?.getAttribute('role')).toBe('status');
  });
});
