// Rubric TypeScript models (Phase 3)
// Mirror AlFalah.Application.DTOs.Rubric.*

export interface RubricStandardDto {
  id: number;
  code: string;
  textAr: string;
  sortOrder: number;
}

export interface RubricDomainDto {
  id: number;
  code: string;
  nameAr: string;
  sortOrder: number;
  standards: RubricStandardDto[];
}

export interface RubricVersionDto {
  id: number;
  versionNumber: number;
  isActive: boolean;
  createdAt: string;
  notes: string | null;
  domains: RubricDomainDto[];
}

export interface RubricVersionListDto {
  id: number;
  versionNumber: number;
  isActive: boolean;
  createdAt: string;
  notes: string | null;
  domainCount: number;
  standardCount: number;
}

// Write DTOs
export interface RubricStandardWriteDto {
  code: string;
  textAr: string;
  sortOrder: number;
}

export interface RubricDomainWriteDto {
  code: string;
  nameAr: string;
  sortOrder: number;
  standards: RubricStandardWriteDto[];
}

export interface CreateRubricVersionDto {
  notes?: string;
  domains: RubricDomainWriteDto[];
}

// Score scale
export interface ScoreScaleEntryDto {
  score: number;
  labelAr: string;
}

export interface PerformanceLevelDto {
  labelAr: string;
  minScore: number;
  isLessThan: boolean;
}

export interface ScoreScaleDto {
  scores: ScoreScaleEntryDto[];
  performanceLevels: PerformanceLevelDto[];
}
