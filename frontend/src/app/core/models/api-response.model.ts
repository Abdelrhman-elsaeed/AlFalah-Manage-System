export interface ApiResponse<T = void> {
  isSuccess: boolean;
  message: string;
  data?: T;
  errors: string[];
}
