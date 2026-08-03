import { PagedResult } from './phase2.models';

export enum StudentAnalyzerProvider {
  Groq = 1,
  Gemini = 2,
  OpenRouter = 3
}

export enum StudentAnalyzerFileKind {
  Pdf = 1,
  Spreadsheet = 2,
  Csv = 3
}

export interface StudentAnalyzerCapabilities {
  canAccess: boolean;
  canDelegate: boolean;
  canManageSettings: boolean;
  schoolId: number | null;
  schoolName: string | null;
}

export interface StudentAnalyzerDelegate {
  userId: string;
  fullName: string;
  username: string;
  roles: string[];
  isGranted: boolean;
}

export interface StudentAnalyzerSettings {
  activeProvider: StudentAnalyzerProvider;
  hasGroqApiKey: boolean;
  groqModel: string;
  hasGeminiApiKey: boolean;
  geminiModel: string;
  hasOpenRouterApiKey: boolean;
  openRouterModel: string;
  updatedAt: string | null;
  updatedByFullName: string | null;
}

export interface UpdateStudentAnalyzerSettingsRequest {
  activeProvider: StudentAnalyzerProvider;
  groqApiKey: string | null;
  clearGroqApiKey: boolean;
  groqModel: string | null;
  geminiApiKey: string | null;
  clearGeminiApiKey: boolean;
  geminiModel: string | null;
  openRouterApiKey: string | null;
  clearOpenRouterApiKey: boolean;
  openRouterModel: string | null;
}

export interface StudentAnalyzerModel {
  id: string;
  name: string;
  description: string | null;
  contextLength: number | null;
  isFree: boolean;
}

export interface StudentAnalyzerFile {
  id: number;
  originalFileName: string;
  contentType: string;
  extension: string;
  fileKind: StudentAnalyzerFileKind;
  sizeBytes: number;
  uploadedByFullName: string;
  uploadedAt: string;
  analysisCount?: number;
  lastAnalyzedAt?: string | null;
}

export interface StudentAnalyzerDataPoint {
  column: string;
  value: string;
  numericValue: number | null;
}

export interface StudentAnalyzerSelectedData {
  grants: StudentAnalyzerDataPoint[];
  deductions: StudentAnalyzerDataPoint[];
}

export interface StudentAnalyzerAnalysis {
  id: number;
  sourceFileId: number;
  sourceFileName: string;
  studentName: string;
  grantTotal: number;
  deductionTotal: number;
  selectedData: StudentAnalyzerSelectedData;
  analysisText: string;
  provider: StudentAnalyzerProvider;
  model: string;
  createdByFullName: string;
  createdAt: string;
}

export interface StudentAnalyzerReportListItem {
  id: number;
  sourceFileId: number;
  sourceFileName: string;
  studentName: string;
  grantTotal: number;
  deductionTotal: number;
  provider: StudentAnalyzerProvider;
  model: string;
  createdByFullName: string;
  createdAt: string;
}

export interface AnalyzeStudentRequest {
  sourceFileId: number;
  studentName: string;
  grants: StudentAnalyzerDataPoint[];
  deductions: StudentAnalyzerDataPoint[];
}

export interface StudentAnalyzerFileQuery {
  page: number;
  pageSize: number;
  search?: string;
  fileKind?: StudentAnalyzerFileKind;
  uploadedFrom?: string;
  uploadedTo?: string;
}

export interface StudentAnalyzerReportQuery {
  page: number;
  pageSize: number;
  search?: string;
  sourceFileId?: number;
  provider?: StudentAnalyzerProvider;
  createdFrom?: string;
  createdTo?: string;
}

export type StudentAnalyzerFilePage = PagedResult<StudentAnalyzerFile>;
export type StudentAnalyzerReportPage = PagedResult<StudentAnalyzerReportListItem>;

