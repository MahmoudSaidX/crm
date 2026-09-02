/** Safe-default-backed branding shown by both app shells before any load completes. */
export interface EffectiveBranding {
  readonly organizationDisplayNameEn: string;
  readonly organizationDisplayNameAr: string | null;
  readonly productDisplayNameEn: string;
  readonly productDisplayNameAr: string | null;
  readonly themeTokens: Readonly<Record<string, string>>;
  readonly primaryLogoUrl: string | null;
  readonly compactLogoUrl: string | null;
  readonly faviconUrl: string | null;
  readonly isDefault: boolean;
}

export const DEFAULT_BRANDING: EffectiveBranding = {
  organizationDisplayNameEn: 'Squad CRM',
  organizationDisplayNameAr: null,
  productDisplayNameEn: 'Squad CRM',
  productDisplayNameAr: null,
  themeTokens: {},
  primaryLogoUrl: null,
  compactLogoUrl: null,
  faviconUrl: null,
  isDefault: true,
};
