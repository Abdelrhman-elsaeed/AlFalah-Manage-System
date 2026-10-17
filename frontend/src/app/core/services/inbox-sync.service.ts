import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { DestroyRef, Injectable, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Subject, auditTime, firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SUPPRESS_ERROR_TOAST } from '../http/http-context.tokens';
import { ApiResponse } from '../models/api-response.model';
import { AuthService } from './auth.service';

export interface InboxState {
  readonly unreadMessages: number | null;
  readonly unreadConversations: number | null;
  readonly unreadNotifications: number | null;
  readonly syncCursor: string;
  readonly asOfUtc: string;
  readonly realtimeAvailable: boolean;
}
interface Changes {
  readonly items: readonly { eventId: string; sequence: number }[];
  readonly nextCursor: string;
  readonly hasMore: boolean;
  readonly resyncRequired: boolean;
}

/** One connection and recovery loop per signed-in tab. REST remains authoritative. */
@Injectable({ providedIn: 'root' })
export class InboxSyncService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly context = new HttpContext().set(SUPPRESS_ERROR_TOAST, true);
  private readonly requests = new Subject<void>();
  private readonly invalidations = new Subject<void>();
  private readonly stateValue = signal<InboxState | null>(null);
  readonly state = this.stateValue.asReadonly();
  readonly unreadMessages = computed(() => this.state()?.unreadMessages ?? 0);
  readonly unreadNotifications = computed(() => this.state()?.unreadNotifications ?? 0);
  readonly status = signal<'connecting' | 'connected' | 'recovering' | 'offline'>('connecting');
  readonly changes$ = this.invalidations.asObservable();
  private connection: HubConnection | null = null;
  private generation = 0;
  private identity = '';
  private cursor: string | null = null;
  private busyGeneration: number | null = null;
  private retryAt = 0;
  private failures = 0;
  private lastRefresh = 0;
  private pending = false;

  constructor() {
    this.requests.pipe(auditTime(150), takeUntilDestroyed(this.destroyRef)).subscribe(() => void this.synchronize());
    effect(() => {
      const user = this.auth.currentUser();
      const identity = user?.activeSchoolId && (user.permissions.includes('Messaging.ViewOwn')
        || user.permissions.includes('Notification.ViewOwn'))
        ? `${user.userId}:${user.activeSchoolId}:${[...user.roles].sort().join(',')}:${[...user.permissions].sort().join(',')}` : '';
      if (identity === this.identity) return;
      this.identity = identity;
      this.generation++;
      const previous = this.connection;
      this.connection = null;
      if (previous) void previous.stop();
      this.cursor = null;
      this.failures = 0;
      this.retryAt = 0;
      this.stateValue.set(null);
      this.status.set('connecting');
      if (identity) this.refresh();
    }, { allowSignalWrites: true });
    if (typeof window !== 'undefined') {
      const focus = () => this.refresh();
      const online = () => { this.retryAt = 0; this.refresh(); };
      const visibility = () => { if (!document.hidden) this.refresh(); };
      window.addEventListener('focus', focus);
      window.addEventListener('online', online);
      document.addEventListener('visibilitychange', visibility);
      // This root service owns the only inbox fallback timer. Hidden tabs do no polling.
      const interval = window.setInterval(() => {
        if (!this.identity || document.hidden) return;
        if (Date.now() - this.lastRefresh >= 30_000) this.refresh();
        if (this.state()?.realtimeAvailable && Date.now() >= this.retryAt
          && this.connection?.state !== HubConnectionState.Connected) void this.connect(this.generation);
      }, 1000);
      this.destroyRef.onDestroy(() => {
        this.generation++;
        window.clearInterval(interval);
        window.removeEventListener('focus', focus);
        window.removeEventListener('online', online);
        document.removeEventListener('visibilitychange', visibility);
        if (this.connection) void this.connection.stop();
      });
    }
  }

  refresh(): void { this.requests.next(); }
  reset(): void { this.stateValue.set(null); this.cursor = null; }

  private current(generation: number): boolean { return generation === this.generation && !!this.identity; }

  private async synchronize(): Promise<void> {
    const generation = this.generation;
    if (!this.current(generation) || (typeof document !== 'undefined' && document.hidden)) return;
    if (this.busyGeneration === generation) { this.pending = true; return; }
    this.busyGeneration = generation;
    this.pending = false;
    try {
      // Drain bounded pages; never skip an unconsumed page by adopting a later snapshot.
      for (let page = 0; this.cursor && page < 10; page++) {
        const response = await firstValueFrom(this.http.get<ApiResponse<Changes>>(
          `${environment.apiUrl}/api/v1/inbox/changes`, { context: this.context,
            params: new HttpParams().set('after', this.cursor).set('limit', 100) }));
        if (!this.current(generation)) return;
        if (!response.isSuccess || !response.data || response.data.resyncRequired) { this.cursor = null; break; }
        this.cursor = response.data.nextCursor;
        if (!response.data.hasMore) break;
        if (page === 9) { this.pending = true; return; }
      }
      const response = await firstValueFrom(this.http.get<ApiResponse<InboxState>>(
        `${environment.apiUrl}/api/v1/inbox/state`, { context: this.context }));
      if (!this.current(generation)) return;
      if (!response.isSuccess || !response.data) { this.stateValue.set(null); this.status.set('offline'); return; }
      this.stateValue.set(response.data);
      this.cursor = response.data.syncCursor;
      this.lastRefresh = Date.now();
      this.invalidations.next();
      if (response.data.realtimeAvailable) void this.connect(generation);
      else this.status.set('recovering');
    } catch {
      if (this.current(generation)) { this.status.set('offline'); this.lastRefresh = Date.now(); }
    } finally {
      if (this.busyGeneration === generation) this.busyGeneration = null;
      if (this.current(generation) && this.pending) this.refresh();
    }
  }

  private async connect(generation: number): Promise<void> {
    if (!this.current(generation) || !this.state()?.realtimeAvailable || Date.now() < this.retryAt) return;
    let connection = this.connection;
    if (!connection) {
      connection = new HubConnectionBuilder().withUrl(`${environment.apiUrl}/hubs/inbox`, {
        accessTokenFactory: () => this.auth.getAccessToken() ?? ''
      }).configureLogging(LogLevel.None).build();
      connection.on('InboxChanged.v1', () => { if (this.current(generation)) this.refresh(); });
      connection.onclose(() => {
        if (!this.current(generation)) return;
        this.status.set('recovering'); this.scheduleRetry(); this.refresh();
      });
      this.connection = connection;
    }
    if (connection.state !== HubConnectionState.Disconnected) return;
    this.status.set('connecting');
    try {
      await connection.start();
      if (!this.current(generation)) { await connection.stop(); return; }
      this.failures = 0;
      this.status.set('connected');
      this.refresh(); // Covers commits between the REST snapshot and hub subscription.
    } catch {
      if (this.current(generation)) { this.status.set('recovering'); this.scheduleRetry(); }
    }
  }

  private scheduleRetry(): void {
    this.retryAt = Date.now() + Math.min(30_000, 1000 * 2 ** Math.min(this.failures++, 5)) + Math.random() * 500;
  }
}
