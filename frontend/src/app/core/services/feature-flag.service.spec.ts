import { TestBed } from '@angular/core/testing';
import { FeatureFlagService } from './feature-flag.service';
import { environment } from '../../../environments/environment';

describe('FeatureFlagService', () => {
  let service: FeatureFlagService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [FeatureFlagService]
    });
    service = TestBed.inject(FeatureFlagService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should expose the visitsV2 value configured for the active environment', () => {
    expect(service.isVisitsV2Enabled()).toBe(environment.featureFlags.visitsV2);
    expect(service.getAllFlags().visitsV2).toBe(environment.featureFlags.visitsV2);
  });
});
