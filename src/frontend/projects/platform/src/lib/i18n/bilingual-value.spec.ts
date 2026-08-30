import { isCompleteBilingualValue } from './bilingual-value';

describe('isCompleteBilingualValue', () => {
  it('accepts independent non-blank Arabic and English values', () => {
    expect(isCompleteBilingualValue({ arabicValue: 'المبيعات', englishValue: 'Sales' })).toBeTrue();
  });

  it('does not substitute one language for a blank other language', () => {
    expect(isCompleteBilingualValue({ arabicValue: '', englishValue: 'Sales' })).toBeFalse();
    expect(isCompleteBilingualValue({ arabicValue: 'المبيعات', englishValue: '  ' })).toBeFalse();
  });
});
