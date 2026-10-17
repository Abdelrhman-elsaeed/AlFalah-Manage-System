import { TestBed, fakeAsync, flushMicrotasks } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { Subject } from 'rxjs';
import { AuthService } from './auth.service';
import { InboxSyncService } from './inbox-sync.service';
import { NotificationCenterService } from './notification-center.service';
import { provideRouter } from '@angular/router';
import { WebPushService } from './web-push.service';
import { NotificationCenterComponent } from '../../shared/components/notification-center/notification-center.component';

describe('NotificationCenterService', () => {
  let http: HttpTestingController;
  const user = signal<any>(null), changes = new Subject<void>();
  const account = (school: number) => ({ userId: 'synthetic', activeSchoolId: school, roles: ['SchoolManager'], permissions: ['Complaint.View'] });
  beforeEach(() => {
    user.set(account(1));
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule], providers: [
      { provide: AuthService, useValue: { currentUser: user } },
      { provide: InboxSyncService, useValue: { changes$: changes, refresh: () => undefined } }
    ] }); http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { http.verify(); TestBed.resetTestingModule(); });
  it('discards old school counts and history and resets on logout', fakeAsync(() => {
    const center = TestBed.inject(NotificationCenterService); TestBed.flushEffects();
    const old = http.expectOne(r => r.url.endsWith('/notification-center/state'));
    let oldItems: any; void center.page().then(page => oldItems = page.items);
    const oldHistory = http.expectOne(r => r.url.endsWith('/notification-center'));
    user.set(account(2)); TestBed.flushEffects();
    http.expectOne(r => r.url.endsWith('/notification-center/state')).flush({ isSuccess: true, data: { unreadCount: 121, available: true } }); flushMicrotasks();
    old.flush({ isSuccess: true, data: { unreadCount: 3, available: true } });
    oldHistory.flush({ isSuccess: true, data: { items: [{ id: 'old-private-content' }], nextCursor: null } }); flushMicrotasks();
    expect(center.unread()).toBe(121); expect(oldItems).toEqual([]);
    user.set(null); TestBed.flushEffects(); expect(center.unread()).toBe(0); expect(center.available()).toBeFalse();
  }));
  it('uses explicit read commands and sends bounded category/unread cursors', fakeAsync(() => {
    const center = TestBed.inject(NotificationCenterService); TestBed.flushEffects();
    http.expectOne(r => r.url.endsWith('/state')).flush({ isSuccess: true, data: { unreadCount: 8, available: true } }); flushMicrotasks();
    void center.page('approvals', true, 'protected-cursor');
    const page = http.expectOne(r => r.url.endsWith('/notification-center'));
    expect(page.request.params.get('limit')).toBe('30'); expect(page.request.params.get('category')).toBe('approvals');
    expect(page.request.params.get('unread')).toBe('true'); expect(page.request.params.get('before')).toBe('protected-cursor');
    page.flush({ isSuccess: true, data: { items: [], nextCursor: null } }); flushMicrotasks();
    expect(center.unread()).toBe(8);
    void center.read('entry'); http.expectOne(r => r.url.endsWith('/entry/read') && r.method === 'POST').flush({isSuccess:true,data:true}); flushMicrotasks();
    http.expectOne(r => r.url.endsWith('/state')).flush({isSuccess:true,data:{unreadCount:7,available:true}}); flushMicrotasks();
    expect(center.unread()).toBe(7);
  }));
});

describe('NotificationCenterComponent context privacy', () => {
  it('clears already rendered previews immediately while the new context request is pending', fakeAsync(() => {
    const contextVersion = signal(1);
    const page = jasmine.createSpy('page').and.returnValues(Promise.resolve({ items: [{ id: 'private-entry', category: 'messages',
      title: 'Private previous-school preview', preview: 'Synthetic confidential text', createdAtUtc: new Date().toISOString(), taskState: 'info', readAtUtc: null }], nextCursor: null }), new Promise(() => {}));
    TestBed.configureTestingModule({ imports: [NotificationCenterComponent], providers: [provideRouter([]),
      { provide: NotificationCenterService, useValue: { contextVersion, revision: signal(1), available: signal(true), unread: signal(1), page, icon: () => 'pi-bell', category: () => 'Messages' } },
      { provide: InboxSyncService, useValue: { status: signal('live') } },
      { provide: WebPushService, useValue: { config: signal(null), controlsOpen: signal(false) } }
    ] });
    const fixture = TestBed.createComponent(NotificationCenterComponent); fixture.componentInstance.history = true;
    fixture.detectChanges(); flushMicrotasks(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Private previous-school preview');
    contextVersion.set(2); TestBed.flushEffects(); fixture.detectChanges();
    expect(page).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.textContent).not.toContain('Private previous-school preview');
    expect(fixture.componentInstance.entries()).toEqual([]);
    TestBed.resetTestingModule();
  }));
});
