import { InjectionToken, Provider } from '@angular/core';
import { SupportedLocale } from './locale';

export type TranslationKey = string;
export type TranslationDictionary = Readonly<Record<TranslationKey, string>>;
export type TranslationResources = Readonly<Record<SupportedLocale, TranslationDictionary>>;

export const TRANSLATION_RESOURCES = new InjectionToken<readonly TranslationResources[]>(
  'SQUAD_CRM_TRANSLATION_RESOURCES',
);

export function provideTranslations(resources: TranslationResources): Provider {
  return { provide: TRANSLATION_RESOURCES, useValue: resources, multi: true };
}
