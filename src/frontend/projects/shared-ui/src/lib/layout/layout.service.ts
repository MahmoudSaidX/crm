import { Injectable, computed, effect, signal } from '@angular/core';
import { Subject } from 'rxjs';

export type MenuMode = 'static' | 'overlay';

export interface LayoutConfig {
  readonly darkTheme: boolean;
  readonly menuMode: MenuMode;
}

export interface LayoutState {
  readonly staticMenuDesktopInactive: boolean;
  readonly overlayMenuActive: boolean;
  readonly staticMenuMobileActive: boolean;
  readonly menuHoverActive: boolean;
}

export interface MenuChangeEvent {
  readonly key: string;
  readonly routeEvent?: boolean;
}

const THEME_STORAGE_KEY = 'squad-crm.theme';
const DARK_MODE_CLASS = 'app-dark';
const DESKTOP_BREAKPOINT = '(min-width: 992px)';

/**
 * Owns the Sakai-style application layout state: menu mode, sidebar visibility and
 * the light/dark colour scheme. Adapted from PrimeNG Sakai (MIT, PrimeTek) 20.0.0.
 */
@Injectable({ providedIn: 'root' })
export class LayoutService {
  readonly layoutConfig = signal<LayoutConfig>({
    darkTheme: readStoredDarkTheme(),
    menuMode: 'static',
  });
  readonly layoutState = signal<LayoutState>({
    staticMenuDesktopInactive: false,
    overlayMenuActive: false,
    staticMenuMobileActive: false,
    menuHoverActive: false,
  });

  readonly isDarkTheme = computed(() => this.layoutConfig().darkTheme);
  readonly isOverlay = computed(() => this.layoutConfig().menuMode === 'overlay');
  readonly isSidebarActive = computed(
    () => this.layoutState().overlayMenuActive || this.layoutState().staticMenuMobileActive,
  );

  private readonly menuSource = new Subject<MenuChangeEvent>();
  private readonly resetSource = new Subject<void>();
  private readonly overlayOpen = new Subject<void>();
  readonly menuSource$ = this.menuSource.asObservable();
  readonly resetSource$ = this.resetSource.asObservable();
  readonly overlayOpen$ = this.overlayOpen.asObservable();

  constructor() {
    effect(() => {
      const dark = this.layoutConfig().darkTheme;
      document.documentElement.classList.toggle(DARK_MODE_CLASS, dark);
      writeStoredDarkTheme(dark);
    });

    effect(() => {
      // Sakai locks page scrolling while the off-canvas menu covers the content.
      document.body.classList.toggle('blocked-scroll', this.isSidebarActive());
    });
  }

  toggleDarkMode(): void {
    this.layoutConfig.update((config) => ({ ...config, darkTheme: !config.darkTheme }));
  }

  onMenuToggle(): void {
    if (this.isOverlay()) {
      this.layoutState.update((state) => ({
        ...state,
        overlayMenuActive: !state.overlayMenuActive,
      }));
      if (this.layoutState().overlayMenuActive) {
        this.overlayOpen.next();
      }
      return;
    }

    if (isDesktop()) {
      this.layoutState.update((state) => ({
        ...state,
        staticMenuDesktopInactive: !state.staticMenuDesktopInactive,
      }));
      return;
    }

    this.layoutState.update((state) => ({
      ...state,
      staticMenuMobileActive: !state.staticMenuMobileActive,
    }));
    if (this.layoutState().staticMenuMobileActive) {
      this.overlayOpen.next();
    }
  }

  hideMenu(): void {
    this.layoutState.update((state) => ({
      ...state,
      overlayMenuActive: false,
      staticMenuMobileActive: false,
      menuHoverActive: false,
    }));
  }

  onMenuStateChange(event: MenuChangeEvent): void {
    this.menuSource.next(event);
  }

  reset(): void {
    this.resetSource.next();
  }
}

function isDesktop(): boolean {
  return window.matchMedia(DESKTOP_BREAKPOINT).matches;
}

function readStoredDarkTheme(): boolean {
  try {
    return localStorage.getItem(THEME_STORAGE_KEY) === 'dark';
  } catch {
    // Private browsing or blocked storage: fall back to the light scheme.
    return false;
  }
}

function writeStoredDarkTheme(dark: boolean): void {
  try {
    localStorage.setItem(THEME_STORAGE_KEY, dark ? 'dark' : 'light');
  } catch {
    // Persisting the preference is best-effort; the session still works without it.
  }
}
