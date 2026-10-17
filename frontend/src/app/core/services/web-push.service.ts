import { HttpClient, HttpContext } from '@angular/common/http';
import { DestroyRef, Injectable, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { SwPush, SwUpdate } from '@angular/service-worker';
import { firstValueFrom, timeout } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { SUPPRESS_ERROR_TOAST } from '../http/http-context.tokens';
import { AuthService } from './auth.service';

interface Config { enabled: boolean; pwaUiEnabled: boolean; publicKey?: string; vapidKeyId?: string; }
export interface PushDevice { id: string; installationId: string; bindingVersion: number; confirmedAtUtc: string; }
@Injectable({ providedIn: 'root' })
export class WebPushService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly sw = inject(SwPush);
  private readonly updates = inject(SwUpdate);
  private readonly destroy = inject(DestroyRef);
  private readonly context = new HttpContext().set(SUPPRESS_ERROR_TOAST, true);
  private readonly url = `${environment.apiUrl}/api/v1/push`;
  readonly controlsOpen = signal(false);
  readonly config = signal<Config | null>(null);
  readonly busy = signal(false);
  readonly enabled = signal(false);
  readonly message = signal('');
  readonly devices = signal<PushDevice[]>([]);
  readonly updateReady = signal(false);
  readonly unrecoverable = signal(false);
  readonly installAvailable = signal(false);
  private installPrompt: (Event & { prompt(): Promise<void>; userChoice: Promise<{outcome: string}> }) | null = null;
  readonly installationId = this.installation();
  private identity = '';
  private generation = 0;
  private deviceId: string | null = null;
  private subscription: PushSubscription | null = null;
  private subscriptionReady = false;
  private readonly supported = typeof window !== 'undefined' && window.isSecureContext && 'Notification' in window && 'PushManager' in window;
  private readonly iosInstall = typeof navigator !== 'undefined' && /iPad|iPhone|iPod/.test(navigator.userAgent)
    && !window.matchMedia('(display-mode: standalone)').matches && !(navigator as Navigator & {standalone?: boolean}).standalone;
  readonly canEnable = computed(() => this.supported && !this.iosInstall && this.sw.isEnabled && !!this.config()?.enabled && !this.enabled()
    && Notification.permission !== 'denied');
  readonly explanation = computed(() => {
    if (!this.supported) return 'هذا المتصفح لا يدعم إشعارات الجهاز. تبقى إشعاراتك متاحة داخل التطبيق.';
    if (this.iosInstall) return 'على iPhone أو iPad: افتح الموقع في Safari، ثم مشاركة ← إضافة إلى الشاشة الرئيسية. افتح التطبيق من أيقونته لتفعيل الإشعارات.';
    if (Notification.permission === 'denied') return 'الإشعارات محظورة من إعدادات المتصفح. يمكنك السماح بها من إعدادات الموقع ثم العودة هنا.';
    if (!this.config()?.enabled || !this.sw.isEnabled) return 'إشعارات الجهاز غير مفعّلة في هذه البيئة حاليًا. تبقى إشعاراتك متاحة داخل التطبيق.';
    if (this.enabled()) return 'إشعارات هذا المتصفح مفعّلة للمدرسة والحساب الحاليين. محتوى شاشة القفل عام لحماية الخصوصية.';
    return 'اسمح بتنبيه عام عند وجود تحديث من المدرسة. قد تؤثر إعدادات الجهاز والاتصال في وصوله. يمكنك الإيقاف في أي وقت.';
  });
  constructor() {
    this.sw.subscription.pipe(takeUntilDestroyed(this.destroy)).subscribe(value => {
      this.subscription = value; this.subscriptionReady = true;
      if (this.identity) void this.reconcile();
    });
    effect(() => {
      const user = this.auth.currentUser();
      const identity = user?.activeSchoolId ? `${user.userId}:${user.activeSchoolId}` : '';
      if (identity === this.identity) return;
      this.identity = identity; this.generation++; this.enabled.set(false); this.devices.set([]); this.config.set(null); this.deviceId = null;
      if (identity) void this.reconcile();
    }, { allowSignalWrites: true });
    this.auth.beforeLogout = () => this.disable();
    const resume = () => { if (this.identity && !document.hidden) void this.reconcile(); };
    window.addEventListener('focus', resume); window.addEventListener('online', resume);
    const install = (event: Event) => { event.preventDefault(); this.installPrompt = event as typeof this.installPrompt; this.installAvailable.set(true); };
    window.addEventListener('beforeinstallprompt', install);
    this.destroy.onDestroy(() => { window.removeEventListener('focus', resume); window.removeEventListener('online', resume); window.removeEventListener('beforeinstallprompt', install); });
    this.updates.versionUpdates.pipe(takeUntilDestroyed(this.destroy)).subscribe(event => { if (event.type === 'VERSION_READY') this.updateReady.set(true); });
    this.updates.unrecoverable.pipe(takeUntilDestroyed(this.destroy)).subscribe(() => this.unrecoverable.set(true));
  }
  private installation(): string {
    try { const saved = localStorage.getItem('alfalah_push_installation'); if (saved) return saved;
      const value = crypto.randomUUID(); localStorage.setItem('alfalah_push_installation', value); return value;
    } catch { return crypto.randomUUID(); }
  }
  private async reconcile(): Promise<void> {
    const generation = this.generation;
    try {
      const response = await firstValueFrom(this.http.get<ApiResponse<Config>>(`${this.url}/config`, { context: this.context }));
      if (generation !== this.generation) return;
      this.config.set(response.data ?? null);
      const devices = await firstValueFrom(this.http.get<ApiResponse<PushDevice[]>>(`${this.url}/subscriptions`, { context: this.context }));
      if (generation !== this.generation) return;
      this.devices.set(devices.data ?? []);
      const device = this.devices().find(x => x.installationId === this.installationId);
      this.deviceId = device?.id ?? null;
      this.enabled.set(!!device && !!this.subscription && this.supported && Notification.permission === 'granted');
      // Never auto-enroll an endpoint into a different account, and never ask permission on resume.
      if (device && this.subscriptionReady && (!this.subscription || this.supported && Notification.permission === 'denied')) await this.revoke(device.id);
    } catch { if (generation === this.generation) this.message.set('تعذر التحقق من حالة الجهاز. أعد المحاولة عند الاتصال.'); }
  }
  async enable(): Promise<void> {
    if (!this.canEnable() || this.busy()) return;
    const generation = this.generation, config = this.config();
    let registering = false;
    this.busy.set(true); this.message.set('');
    try {
      if (this.subscription && !this.deviceId) await this.subscription.unsubscribe();
      const subscription = await this.sw.requestSubscription({ serverPublicKey: config!.publicKey! });
      if (generation !== this.generation) { await subscription.unsubscribe(); return; }
      registering = true;
      const response = await firstValueFrom(this.http.post<ApiResponse<PushDevice>>(`${this.url}/subscriptions`, {
        installationId: this.installationId, vapidKeyId: config!.vapidKeyId, subscription: subscription.toJSON()
      }, { context: this.context }));
      if (!response.isSuccess || !response.data) { await subscription.unsubscribe(); throw new Error('registration'); }
      if (generation !== this.generation) { await subscription.unsubscribe(); return; }
      this.deviceId = response.data.id; this.enabled.set(true); this.message.set('تم تفعيل إشعارات هذا الجهاز.'); await this.loadDevices();
    } catch (error: unknown) {
      if (generation !== this.generation) return;
      this.enabled.set(false);
      this.message.set(registering
        ? 'تعذر تسجيل الجهاز لدى المدرسة. تحقق من الاتصال والحساب النشط ثم أعد المحاولة.'
        : this.subscriptionFailure(error));
    }
    finally { this.busy.set(false); }
  }
  private subscriptionFailure(error: unknown): string {
    const name = error instanceof DOMException ? error.name : '';
    if (name === 'NotAllowedError') return 'لم يسمح المتصفح بالاشتراك. راجع إذن إشعارات الموقع وإشعارات Chrome في إعدادات الهاتف، ثم أعد المحاولة.';
    if (name === 'AbortError' || name === 'NetworkError') return 'تعذر اتصال المتصفح بخدمة الإشعارات. جرّب شبكة أخرى ثم أعد المحاولة.';
    if (name === 'InvalidAccessError' || name === 'InvalidStateError') return 'تعذر استخدام إعدادات الاشتراك الحالية. حدّث التطبيق ثم أعد تفعيل إشعارات الجهاز.';
    if (name === 'NotSupportedError') return 'تعذر إنشاء اشتراك الإشعارات في هذا المتصفح. افتح الرابط في أحدث إصدار من Chrome ثم أعد المحاولة.';
    return 'لم يكتمل اشتراك المتصفح بالإشعارات. أعد فتح التطبيق ثم حاول مرة أخرى.';
  }
  async disable(): Promise<void> {
    this.busy.set(true);
    try {
      if (this.deviceId) await firstValueFrom(this.http.delete(`${this.url}/subscriptions/${this.deviceId}`, { context: this.context }).pipe(timeout(5000)));
      if (this.subscription) await this.subscription.unsubscribe();
      this.deviceId = null; this.enabled.set(false); this.message.set('تم إيقاف إشعارات هذا الجهاز.');
    } catch {
      if (this.subscription) await this.subscription.unsubscribe().catch(() => false);
      this.enabled.set(false); this.message.set('أُوقف الاشتراك محليًا. لم يتأكد الإيقاف على الخادم؛ قد يصل تنبيه عام سبق إرساله.');
    } finally { this.busy.set(false); }
  }
  async loadDevices(): Promise<void> { await this.reconcile(); }
  async revoke(id: string): Promise<void> {
    if (id === this.deviceId) { await this.disable(); await this.reconcile(); return; }
    try { await firstValueFrom(this.http.delete(`${this.url}/subscriptions/${id}`)); this.devices.update(x => x.filter(d => d.id !== id)); }
    catch { this.message.set('تعذر إيقاف الجهاز. أعد المحاولة.'); }
  }
  reload(): void {
    // A deliberate prompt protects unsent fields across all existing domain screens.
    if (window.confirm('احفظ أي مسودة أو بيانات غير محفوظة قبل تحديث التطبيق. هل تريد إعادة التحميل الآن؟')) window.location.reload();
  }
  async install(): Promise<void> {
    if (!this.installPrompt || !this.config()?.pwaUiEnabled) return;
    await this.installPrompt.prompt(); await this.installPrompt.userChoice;
    this.installPrompt = null; this.installAvailable.set(false);
  }
}
