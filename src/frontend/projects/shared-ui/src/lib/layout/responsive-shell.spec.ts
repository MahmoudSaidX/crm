import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { providePrimeNG } from 'primeng/config';
import { ResponsiveShell } from './responsive-shell';

describe('ResponsiveShell', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideNoopAnimations(), providePrimeNG({})],
    });
  });

  it('renders reusable landmark navigation and keeps the mobile menu action discoverable', () => {
    const fixture = TestBed.createComponent(ResponsiveShell);
    fixture.componentRef.setInput('title', 'Squad CRM');
    fixture.componentRef.setInput('menuLabel', 'Open navigation menu');
    fixture.componentRef.setInput('closeMenuLabel', 'Close navigation menu');
    fixture.componentRef.setInput('navigationLabel', 'Primary navigation');
    fixture.componentRef.setInput('navigationItems', [
      { label: 'Home', icon: 'pi pi-home', routerLink: '/', exact: true },
    ]);
    fixture.detectChanges();

    const navigation: HTMLElement | null = fixture.nativeElement.querySelector(
      'nav[aria-label="Primary navigation"]',
    );
    const menuButton: HTMLButtonElement | null = fixture.nativeElement.querySelector(
      '[data-testid="mobile-menu-trigger"] button',
    );

    expect(navigation?.textContent).toContain('Home');
    expect(menuButton?.getAttribute('aria-label')).toBe('Open navigation menu');
    expect(fixture.nativeElement.querySelector('main[tabindex="-1"]')).not.toBeNull();
  });

  it('opens the PrimeNG mobile drawer from the menu action', () => {
    const fixture = TestBed.createComponent(ResponsiveShell);
    fixture.componentRef.setInput('title', 'Squad CRM');
    fixture.componentRef.setInput('menuLabel', 'Open navigation menu');
    fixture.componentRef.setInput('closeMenuLabel', 'Close navigation menu');
    fixture.componentRef.setInput('navigationLabel', 'Primary navigation');
    fixture.componentRef.setInput('navigationItems', []);
    fixture.detectChanges();

    fixture.nativeElement.querySelector('[data-testid="mobile-menu-trigger"] button').click();
    fixture.detectChanges();

    expect(document.querySelector('.sc-mobile-navigation')).not.toBeNull();
  });
});
