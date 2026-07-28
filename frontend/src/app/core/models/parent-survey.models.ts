export enum ParentSurveyStatus {
  Draft = 0,
  Published = 1,
  Closed = 2
}

export enum ParentSurveyRating {
  Weak = 1,
  Acceptable = 2,
  Good = 3,
  VeryGood = 4
}

export interface ParentSurveyItem {
  id: number;
  text: string;
  sortOrder: number;
}

export interface ParentSurvey {
  id: number;
  schoolId: number;
  schoolName: string;
  title: string;
  description?: string;
  isTemplate: boolean;
  status: ParentSurveyStatus;
  publicToken?: string;
  submissionCount: number;
  createdAt: string;
  updatedAt: string;
  items: ParentSurveyItem[];
}

export interface SaveParentSurveyRequest {
  schoolId?: number;
  title: string;
  description?: string;
  isTemplate: boolean;
  sourceTemplateId?: number;
  items: Array<{ id?: number; text: string }>;
}

export interface PublishParentSurvey {
  publicToken: string;
  publishedAt: string;
}

export interface PublicParentSurvey {
  title: string;
  description?: string;
  schoolName: string;
  schoolLogoUrl?: string;
  isAcceptingResponses: boolean;
  items: ParentSurveyItem[];
}

export interface SubmitParentSurveyRequest {
  parentName: string;
  mobileNumber: string;
  answers: Array<{
    itemId: number;
    rating: ParentSurveyRating;
    weakReason?: string;
  }>;
}

export interface ParentSurveySubmissionListItem {
  id: number;
  parentName: string;
  mobileNumber: string;
  submittedAt: string;
  autoAdjustedAnswerCount: number;
}

export interface ParentSurveySubmission {
  id: number;
  parentSurveyId: number;
  parentName: string;
  mobileNumber: string;
  submittedAt: string;
  answers: Array<{
    itemId: number;
    itemText: string;
    submittedRating: ParentSurveyRating;
    effectiveRating: ParentSurveyRating;
    weakReason?: string;
    wasAutoAdjusted: boolean;
  }>;
}
