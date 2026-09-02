import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface BrandingAsset {
  readonly kind: string;
  readonly originalFileName: string;
  readonly contentType: string;
  readonly sizeBytes: number;
  readonly createdAtUtc: string;
}

export interface BrandingSettings {
  readonly organizationDisplayNameEn: string;
  readonly organizationDisplayNameAr: string | null;
  readonly productDisplayNameEn: string;
  readonly productDisplayNameAr: string | null;
  readonly themeTokens: Readonly<Record<string, string>>;
  readonly assets: readonly BrandingAsset[];
  readonly updatedAtUtc: string;
  readonly updatedByHandle: string | null;
}

export interface UpdateBrandingSettingsRequest {
  readonly organizationDisplayNameEn: string;
  readonly organizationDisplayNameAr: string | null;
  readonly productDisplayNameEn: string;
  readonly productDisplayNameAr: string | null;
  readonly themeTokens: Readonly<Record<string, string>> | null;
}

export type BrandingLogoKind = 'primary' | 'compact' | 'favicon';

@Injectable({ providedIn: 'root' })
export class BrandingSettingsService {
  private readonly http = inject(HttpClient);

  get(): Promise<BrandingSettings> {
    return firstValueFrom(this.http.get<BrandingSettings>('/api/v1/branding'));
  }

  update(request: UpdateBrandingSettingsRequest): Promise<BrandingSettings> {
    return firstValueFrom(this.http.put<BrandingSettings>('/api/v1/branding', request));
  }

  uploadLogo(kind: BrandingLogoKind, file: File): Promise<BrandingSettings> {
    const formData = new FormData();
    formData.append('file', file);
    return firstValueFrom(
      this.http.post<BrandingSettings>(`/api/v1/branding/logo/${kind}`, formData),
    );
  }

  deleteLogo(kind: BrandingLogoKind): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/api/v1/branding/logo/${kind}`));
  }
}
