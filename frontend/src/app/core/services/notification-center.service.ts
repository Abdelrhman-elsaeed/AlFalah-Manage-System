import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { DestroyRef, Injectable, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SUPPRESS_ERROR_TOAST } from '../http/http-context.tokens';
import { ApiResponse } from '../models/api-response.model';
import { AuthService } from './auth.service';
import { InboxSyncService } from './inbox-sync.service';

export interface CenterItem {
  id: string; category: string; title: string; preview: string; createdAtUtc: string;
  readAtUtc: string | null; taskState: string; sourceType: string; sourceId: number;
}
export interface CenterPage { items: CenterItem[]; nextCursor: string | null; }
export interface CenterTarget { available: boolean; route?: string; query?: Record<string, string>; title?: string; state?: string; }
export const NOTIFICATION_CATEGORIES = [
  { id: '', label: 'كل الأنواع', icon: 'pi-inbox' }, { id: 'messages', label: 'الرسائل', icon: 'pi-comment' },
  { id: 'duty', label: 'المناوبة', icon: 'pi-shield' }, { id: 'students', label: 'شؤون الطلاب', icon: 'pi-graduation-cap' },
  { id: 'approvals', label: 'الاعتمادات', icon: 'pi-file-check' }, { id: 'attendance', label: 'الحضور', icon: 'pi-calendar' },
  { id: 'complaints', label: 'الشكاوى', icon: 'pi-envelope' }, { id: 'cases', label: 'الإحالات والمتابعة', icon: 'pi-folder-open' }
];

@Injectable({ providedIn: 'root' })
export class NotificationCenterService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly sync = inject(InboxSyncService);
  private readonly destroy = inject(DestroyRef);
  private readonly url = `${environment.apiUrl}/api/v1/notification-center`;
  private readonly context = new HttpContext().set(SUPPRESS_ERROR_TOAST, true);
  readonly unread = signal(0);
  readonly available = signal(false);
  readonly revision = signal(0);
  readonly contextVersion = signal(0);
  private generation = 0;
  private countRequest = 0;
  private identity = '';

  constructor() {
    effect(() => {
      const user = this.auth.currentUser();
      const identity = user?.activeSchoolId ? `${user.userId}:${user.activeSchoolId}:${user.roles.join(',')}:${user.permissions.join(',')}` : '';
      if (identity === this.identity) return;
      this.identity = identity; this.generation++; this.unread.set(0); this.available.set(false);
      this.contextVersion.update(x => x + 1); this.revision.update(x => x + 1);
      if (identity) void this.refresh();
    }, { allowSignalWrites: true });
    this.sync.changes$.pipe(takeUntilDestroyed(this.destroy)).subscribe(() => { void this.refresh(); this.revision.update(x => x + 1); });
  }
  async refresh(): Promise<void> {
    const generation = this.generation, request = ++this.countRequest;
    try {
      const response = await firstValueFrom(this.http.get<ApiResponse<{unreadCount: number; available: boolean}>>(`${this.url}/state`, { context: this.context }));
      if (generation !== this.generation || request !== this.countRequest) return;
      this.available.set(!!response.data?.available && response.isSuccess); this.unread.set(response.data?.unreadCount ?? 0);
    } catch { if (generation === this.generation) this.available.set(false); }
  }
  async page(category = '', unread = false, before?: string | null): Promise<CenterPage> {
    const generation = this.generation;
    let params = new HttpParams().set('limit', 30).set('unread', unread);
    if (category) params = params.set('category', category);
    if (before) params = params.set('before', before);
    const response = await firstValueFrom(this.http.get<ApiResponse<CenterPage>>(this.url, { params, context: this.context }));
    if (generation !== this.generation) return { items: [], nextCursor: null };
    if (!response.isSuccess || !response.data) throw new Error('unavailable');
    return response.data;
  }
  async read(id?: string): Promise<void> {
    const response = await firstValueFrom(this.http.post<ApiResponse<boolean>>(`${this.url}/${id ? `${id}/read` : 'read-all'}`, {}));
    if (!response.isSuccess) throw new Error('read');
    this.sync.refresh(); this.revision.update(x => x + 1); await this.refresh();
  }
  async target(id: string): Promise<CenterTarget> {
    const generation = this.generation;
    const response = await firstValueFrom(this.http.get<ApiResponse<CenterTarget>>(`${this.url}/${encodeURIComponent(id)}/target`, { context: this.context }));
    return generation === this.generation && response.isSuccess ? response.data ?? { available: false } : { available: false };
  }
  icon(category: string): string { return NOTIFICATION_CATEGORIES.find(x => x.id === category)?.icon ?? 'pi-bell'; }
  category(category: string): string { return NOTIFICATION_CATEGORIES.find(x => x.id === category)?.label ?? 'إشعار مدرسي'; }
}
