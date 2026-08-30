export interface BilingualValue {
  readonly arabicValue: string;
  readonly englishValue: string;
}

export function isCompleteBilingualValue(value: unknown): value is BilingualValue {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const candidate = value as Partial<BilingualValue>;
  return (
    typeof candidate.arabicValue === 'string' &&
    candidate.arabicValue.trim().length > 0 &&
    typeof candidate.englishValue === 'string' &&
    candidate.englishValue.trim().length > 0
  );
}
