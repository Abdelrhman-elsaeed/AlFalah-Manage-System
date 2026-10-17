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
    user.set({ userId: 'synthetic', activeSchoolId: 1 }); requestSubscription.calls.reset();
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule], providers: [
      { provide: AuthService, useValue: { currentUser: user, beforeLogout: null } },
      { provide: SwPush, useValue: { isEnabled: true, subscription, requestSubscription } },
      { provide: SwUpdate, useValue: { versionUpdates: versions, unrecoverable: new Subject() } }
    ] }); http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { http.verify(); TestBed.resetTestingModule(); });
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
