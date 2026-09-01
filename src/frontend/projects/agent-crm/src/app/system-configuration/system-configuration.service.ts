import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type ConfigurationValueType = 'String' | 'Number' | 'Boolean';

export interface ConfigurationValue {
  readonly key: string;
  readonly valueType: ConfigurationValueType;
  readonly displayNameEn: string;
  readonly displayNameAr: string;
  readonly descriptionEn: string | null;
  readonly descriptionAr: string | null;
  readonly value: string | null;
  readonly hasValue: boolean;
  readonly defaultValue: string;
  readonly isSensitive: boolean;
  readonly requiresRestart: boolean;
  readonly isEditable: boolean;
  readonly minNumber: number | null;
  readonly maxNumber: number | null;
  readonly updatedByHandle: string | null;
  readonly updatedAtUtc: string | null;
}

/**
 * Only the explicitly registered configuration keys returned by the backend
 * catalog are browsable/editable here — this service never lets the UI
 * create arbitrary keys.
 */
@Injectable({ providedIn: 'root' })
export class SystemConfigurationService {
  private readonly http = inject(HttpClient);

  list(): Promise<ConfigurationValue[]> {
    return firstValueFrom(this.http.get<ConfigurationValue[]>('/api/v1/system-configuration'));
  }

  update(key: string, value: string): Promise<ConfigurationValue> {
    return firstValueFrom(
      this.http.put<ConfigurationValue>(`/api/v1/system-configuration/${key}`, { value }),
    );
  }
}
