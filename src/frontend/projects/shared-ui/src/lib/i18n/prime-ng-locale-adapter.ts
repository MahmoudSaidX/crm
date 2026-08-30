import { Injectable, inject } from '@angular/core';
import { PrimeNG } from 'primeng/config';
import { PRIME_NG_TRANSLATIONS } from './common-translations';
import { LanguageSwitcherLocale } from './language-switcher';

@Injectable({ providedIn: 'root' })
export class PrimeNgLocaleAdapter {
  private readonly primeNg = inject(PrimeNG);

  setLocale(locale: LanguageSwitcherLocale): void {
    this.primeNg.setTranslation(PRIME_NG_TRANSLATIONS[locale]);
  }
}
