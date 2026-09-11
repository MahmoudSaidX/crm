import { TestBed } from '@angular/core/testing';
import { LayoutService } from './layout.service';

describe('LayoutService', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove('app-dark');
    TestBed.configureTestingModule({});
  });

  it('toggles the dark class on the document element', () => {
    const service = TestBed.inject(LayoutService);
    TestBed.tick();
    expect(document.documentElement.classList.contains('app-dark')).toBeFalse();

    service.toggleDarkMode();
    TestBed.tick();

    expect(service.isDarkTheme()).toBeTrue();
    expect(document.documentElement.classList.contains('app-dark')).toBeTrue();
    expect(localStorage.getItem('squad-crm.theme')).toBe('dark');

    service.toggleDarkMode();
    TestBed.tick();
    expect(document.documentElement.classList.contains('app-dark')).toBeFalse();
  });

  it('falls back to the light scheme when storage is unavailable', () => {
    spyOn(Storage.prototype, 'getItem').and.throwError('blocked');
    spyOn(Storage.prototype, 'setItem').and.throwError('blocked');

    const service = TestBed.inject(LayoutService);
    TestBed.tick();

    expect(service.isDarkTheme()).toBeFalse();
    expect(() => service.toggleDarkMode()).not.toThrow();
  });

  it('toggles the desktop sidebar in static mode', () => {
    const service = TestBed.inject(LayoutService);
    spyOn(window, 'matchMedia').and.returnValue({ matches: true } as MediaQueryList);

    service.onMenuToggle();

    expect(service.layoutState().staticMenuDesktopInactive).toBeTrue();
    expect(service.isSidebarActive()).toBeFalse();
  });

  it('opens the mobile sidebar below the desktop breakpoint and hides it again', () => {
    const service = TestBed.inject(LayoutService);
    spyOn(window, 'matchMedia').and.returnValue({ matches: false } as MediaQueryList);

    service.onMenuToggle();
    expect(service.layoutState().staticMenuMobileActive).toBeTrue();
    expect(service.isSidebarActive()).toBeTrue();

    service.hideMenu();
    expect(service.isSidebarActive()).toBeFalse();
  });
});
