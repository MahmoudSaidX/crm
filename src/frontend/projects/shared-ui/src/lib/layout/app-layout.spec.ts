import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AppLayout } from './app-layout';
import { LayoutService } from './layout.service';
import { ShellMenuItem } from './shell-menu-item';

@Component({ template: 'home' })
class HomeComponent {}

@Component({ template: 'roles' })
class RolesComponent {}

const MENU: readonly ShellMenuItem[] = [
  {
    label: 'Tickets',
    items: [{ label: 'All tickets', icon: 'pi pi-ticket', routerLink: '/roles' }],
  },
];

describe('AppLayout', () => {
  let fixture: ComponentFixture<AppLayout>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppLayout],
      providers: [
        provideRouter([
          { path: '', component: HomeComponent },
          { path: 'roles', component: RolesComponent },
        ]),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppLayout);
    fixture.componentRef.setInput('title', 'Squad CRM');
    fixture.componentRef.setInput('menuLabel', 'Open menu');
    fixture.componentRef.setInput('closeMenuLabel', 'Close menu');
    fixture.componentRef.setInput('actionsLabel', 'More actions');
    fixture.componentRef.setInput('darkModeLabel', 'Toggle theme');
    fixture.componentRef.setInput('navigationLabel', 'Primary navigation');
    fixture.componentRef.setInput('navigationItems', MENU);
    fixture.componentRef.setInput('footerText', 'Squad CRM');
    fixture.detectChanges();
  });

  it('renders the Sakai shell regions and the focusable content area', () => {
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('.layout-wrapper.layout-static')).not.toBeNull();
    expect(element.querySelector('.layout-topbar')).not.toBeNull();
    expect(element.querySelector('.layout-sidebar')).not.toBeNull();
    expect(element.querySelector('.layout-footer')?.textContent).toContain('Squad CRM');
    expect(element.querySelector('main[tabindex="-1"]')).not.toBeNull();
  });

  it('renders grouped navigation with a section heading per group', () => {
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('.layout-menuitem-root-text')?.textContent).toContain('Tickets');
    expect(element.querySelector('.layout-menu a')?.textContent).toContain('All tickets');
  });

  it('opens the mobile menu from the topbar trigger', () => {
    spyOn(window, 'matchMedia').and.returnValue({ matches: false } as MediaQueryList);
    const element = fixture.nativeElement as HTMLElement;

    element.querySelector<HTMLButtonElement>('[data-testid="mobile-menu-trigger"]')!.click();
    fixture.detectChanges();

    expect(element.querySelector('.layout-wrapper.layout-mobile-active')).not.toBeNull();
  });

  it('closes the mobile menu after navigation', async () => {
    spyOn(window, 'matchMedia').and.returnValue({ matches: false } as MediaQueryList);
    const layout = TestBed.inject(LayoutService);
    layout.onMenuToggle();
    fixture.detectChanges();

    await RouterTestingHarness.create('/roles');
    fixture.detectChanges();

    expect(layout.layoutState().staticMenuMobileActive).toBeFalse();
  });
});
