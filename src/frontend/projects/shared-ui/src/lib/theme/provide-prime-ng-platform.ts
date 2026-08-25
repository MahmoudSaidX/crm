import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import Aura from '@primeng/themes/aura';
import { providePrimeNG } from 'primeng/config';

/**
 * The shared PrimeNG baseline for every Squad CRM surface.
 *
 * This is genuinely shared configuration — the place ADR-009 asks us to centralise
 * theme/token setup — and deliberately not a wrapper layer over PrimeNG components.
 *
 * Scope note: CRM-104 proves integration with a minimal stock preset. Brand palette,
 * design tokens and any dark-mode policy belong to later design/branding work.
 */
export function providePrimeNgPlatform(): EnvironmentProviders {
  return makeEnvironmentProviders([
    // PrimeNG overlays (Dialog, OverlayPanel, …) require the animations providers.
    provideAnimationsAsync(),
    providePrimeNG({
      theme: {
        preset: Aura,
        options: {
          // Keep PrimeNG's generated CSS behind application styles so app-level
          // overrides win without needing specificity hacks.
          cssLayer: { name: 'primeng', order: 'theme, base, primeng' },
        },
      },
      ripple: true,
    }),
  ]);
}
