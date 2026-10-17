import { TestBed, fakeAsync, flushMicrotasks } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { BehaviorSubject, Subject } from 'rxjs';
import { SwPush, SwUpdate } from '@angular/service-worker';
import { AuthService } from './auth.service';
import { WebPushService } from './web-push.service';

describe('WebPushService consent and updates', () => {
  let http: HttpTestingController;
  const user = signal<any>(null), subscription = new BehaviorSubject<PushSubscription|null>(null), versions = new Subject<any>();
  const requestSubscription = jasmine.createSpy('permission-request');
  beforeEach(() => {
    user.set({ userId: 'synthetic', activeSchoolId: 1 }); subscription.next(null); requestSubscription.calls.reset();
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule], providers: [
      { provide: AuthService, useValue: { currentUser: user, beforeLogout: null } },
      { provide: SwPush, useValue: { isEnabled: true, subscription, requestSubscription } },
      { provide: SwUpdate, useValue: { versionUpdates: versions, unrecoverable: new Subject() } }
    ] }); http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { http.verify(); TestBed.resetTestingModule(); });
  function ready(): WebPushService {
    spyOnProperty(Notification, 'permission', 'get').and.returnValue('granted');
    const push = TestBed.inject(WebPushService); TestBed.flushEffects();
    http.expectOne(r => r.url.endsWith('/push/config')).flush({isSuccess:true,data:{enabled:true,pwaUiEnabled:true,publicKey:'public',vapidKeyId:'v1'}}); flushMicrotasks();
    http.expectOne(r => r.url.endsWith('/subscriptions')).flush({isSuccess:true,data:[]}); flushMicrotasks();
    return push;
  }
  it('explains browser push-service failure after granted permission without registering a device', fakeAsync(() => {
    const push = ready();
    requestSubscription.and.callFake(() => Promise.reject(new DOMException('synthetic browser failure', 'AbortError')));
    void push.enable(); flushMicrotasks();
    expect(requestSubscription).toHaveBeenCalledTimes(1);
    http.expectNone(r => r.method === 'POST');
    expect(push.enabled()).toBeFalse(); expect(push.busy()).toBeFalse();
    expect(push.message()).toContain('تعذر اتصال المتصفح بخدمة الإشعارات');
    expect(push.message()).toContain('شبكة أخرى');
  }));
  it('directs permission rejection to phone and site settings rather than claiming server failure', fakeAsync(() => {
    const push = ready();
    requestSubscription.and.callFake(() => Promise.reject(new DOMException('synthetic denied permission', 'NotAllowedError')));
    void push.enable(); flushMicrotasks();
    http.expectNone(r => r.method === 'POST');
    expect(push.enabled()).toBeFalse(); expect(push.busy()).toBeFalse();
    expect(push.message()).toContain('إعدادات الهاتف');
    expect(push.message()).toContain('إذن إشعارات الموقع');
  }));
  it('distinguishes server registration rejection from browser permission', fakeAsync(() => {
    const push = ready();
    requestSubscription.and.callFake(() => Promise.resolve({toJSON: () => ({endpoint:'synthetic-only'}), unsubscribe: () => Promise.resolve(true)}));
    void push.enable(); flushMicrotasks();
    http.expectOne(r => r.method === 'POST' && r.url.endsWith('/subscriptions')).flush({isSuccess:false}, {status:400,statusText:'Bad Request'}); flushMicrotasks();
    expect(push.enabled()).toBeFalse(); expect(push.busy()).toBeFalse();
    expect(push.message()).toContain('تعذر تسجيل الجهاز');
  }));
  it('discards an old subscription failure after the active account changes', fakeAsync(() => {
    const push = ready(); let reject!: (error: unknown) => void;
    requestSubscription.and.callFake(() => new Promise((_, fail) => { reject = fail; }));
    void push.enable();
    user.set({userId:'other-synthetic',activeSchoolId:2}); TestBed.flushEffects();
    http.expectOne(r => r.url.endsWith('/push/config')).flush({isSuccess:true,data:{enabled:true,pwaUiEnabled:true,publicKey:'public',vapidKeyId:'v1'}}); flushMicrotasks();
    http.expectOne(r => r.url.endsWith('/subscriptions')).flush({isSuccess:true,data:[]}); flushMicrotasks();
    push.message.set('حالة الحساب الحالي');
    reject(new DOMException('synthetic old-account failure','AbortError')); flushMicrotasks();
    expect(push.message()).toBe('حالة الحساب الحالي');
    http.expectNone(r => r.method === 'POST');
  }));
  it('never requests permission on login or resume and exposes an update without reloading', fakeAsync(() => {
    const push = TestBed.inject(WebPushService); TestBed.flushEffects();
    http.expectOne(r => r.url.endsWith('/push/config')).flush({isSuccess:true,data:{enabled:true,pwaUiEnabled:true,publicKey:'public',vapidKeyId:'v1'}}); flushMicrotasks();
    http.expectOne(r => r.url.endsWith('/subscriptions')).flush({isSuccess:true,data:[]}); flushMicrotasks();
    expect(requestSubscription).not.toHaveBeenCalled();
    window.dispatchEvent(new Event('focus'));
    http.expectOne(r => r.url.endsWith('/push/config')).flush({isSuccess:true,data:{enabled:true,pwaUiEnabled:true}}); flushMicrotasks();
    http.expectOne(r => r.url.endsWith('/subscriptions')).flush({isSuccess:true,data:[]}); flushMicrotasks();
    expect(requestSubscription).not.toHaveBeenCalled();
    versions.next({ type: 'VERSION_READY' }); expect(push.updateReady()).toBeTrue();
    const confirm = spyOn(window, 'confirm').and.returnValue(false); push.reload();
    expect(confirm).toHaveBeenCalledWith(jasmine.stringMatching('مسودة|مسود|محفوظة'));
  }));
});
