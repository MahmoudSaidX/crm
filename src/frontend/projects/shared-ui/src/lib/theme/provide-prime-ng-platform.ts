import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { definePreset } from '@primeng/themes';
import Aura from '@primeng/themes/aura';
import { providePrimeNG } from 'primeng/config';

/**
 * The shared PrimeNG baseline for every Squad CRM surface.
 *
 * This is genuinely shared configuration — the place ADR-009 asks us to centralise
 * theme/token setup — and deliberately not a wrapper layer over PrimeNG components.
 *
 * The preset follows the PrimeNG Sakai (MIT, PrimeTek) 20.0.0 defaults: an emerald
 * primary ramp over Aura's neutral surfaces, with dark mode driven by the `.app-dark`
 * class that `LayoutService` toggles.
 */
const SakaiPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '{emerald.50}',
      100: '{emerald.100}',
      200: '{emerald.200}',
      300: '{emerald.300}',
      400: '{emerald.400}',
      500: '{emerald.500}',
      600: '{emerald.600}',
      700: '{emerald.700}',
      800: '{emerald.800}',
      900: '{emerald.900}',
      950: '{emerald.950}',
    },
    colorScheme: {
      light: {
        surface: {
          0: '#ffffff',
          50: '{slate.50}',
          100: '{slate.100}',
          200: '{slate.200}',
          300: '{slate.300}',
          400: '{slate.400}',
          500: '{slate.500}',
          600: '{slate.600}',
          700: '{slate.700}',
          800: '{slate.800}',
          900: '{slate.900}',
          950: '{slate.950}',
        },
      },
      dark: {
        surface: {
          0: '#ffffff',
          50: '{zinc.50}',
          100: '{zinc.100}',
          200: '{zinc.200}',
          300: '{zinc.300}',
          400: '{zinc.400}',
          500: '{zinc.500}',
          600: '{zinc.600}',
          700: '{zinc.700}',
          800: '{zinc.800}',
          900: '{zinc.900}',
          950: '{zinc.950}',
        },
      },
    },
  },
});

export function providePrimeNgPlatform(): EnvironmentProviders {
  return makeEnvironmentProviders([
    // PrimeNG overlays (Dialog, OverlayPanel, …) require the animations providers.
    provideAnimationsAsync(),
    providePrimeNG({
      theme: {
        preset: SakaiPreset,
        options: {
          // Dark mode is a class toggle owned by LayoutService, not a media query.
          darkModeSelector: '.app-dark',
          // Keep PrimeNG's generated CSS behind application styles so app-level
          // overrides win without needing specificity hacks.
          cssLayer: { name: 'primeng', order: 'theme, base, primeng' },
        },
      },
      ripple: true,
    }),
  ]);
}
