import { SUPPORTED_LOCALES, directionForLocale, isSupportedLocale } from './locale';

describe('locale primitives', () => {
  it('supports exactly en and ar', () => {
    expect(SUPPORTED_LOCALES).toEqual(['en', 'ar']);
  });

  it('maps ar to rtl and en to ltr', () => {
    expect(directionForLocale('ar')).toBe('rtl');
    expect(directionForLocale('en')).toBe('ltr');
  });

  it('recognises supported locales only', () => {
    expect(isSupportedLocale('en')).toBeTrue();
    expect(isSupportedLocale('ar')).toBeTrue();
    expect(isSupportedLocale('fr')).toBeFalse();
    expect(isSupportedLocale(null)).toBeFalse();
  });
});
