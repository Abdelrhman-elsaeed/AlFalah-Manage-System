import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface PlatformFeatureFlags {
  visitsV2: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class FeatureFlagService {
  private readonly flags: PlatformFeatureFlags = {
    visitsV2: !!environment.featureFlags?.visitsV2
  };

  /**
   * Returns true if Classroom Visits V2 is enabled; false otherwise.
   */
  isVisitsV2Enabled(): boolean {
    return this.flags.visitsV2;
  }

  /**
   * Returns a snapshot of all active feature flags.
   */
  getAllFlags(): Readonly<PlatformFeatureFlags> {
    return { ...this.flags };
  }
}
