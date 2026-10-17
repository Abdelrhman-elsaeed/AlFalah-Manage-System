import { TestBed, fakeAsync, flushMicrotasks, tick } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { NgZone, signal } from '@angular/core';
import { AuthService } from './auth.service';
import { InboxSyncService } from './inbox-sync.service';
import { CurrentUserDto } from '../models/auth.models';

describe('InboxSyncService recovery', () => {
  let http: HttpTestingController;
  const account = (school: number): CurrentUserDto => ({ userId: `user-${school}`, username: 'synthetic', fullName: 'اختبار',
    activeSchoolId: school, preferredLanguage: 'ar', roles: ['Instructor'], permissions: ['Messaging.ViewOwn'] });
  const user = signal<CurrentUserDto | null>(null);
  const response = (cursor: string, unread = 121) => ({ isSuccess: true, errors: [], data: {
    unreadMessages: unread, unreadConversations: unread, unreadNotifications: null, syncCursor: cursor,
    asOfUtc: new Date().toISOString(), realtimeAvailable: false
  } });

  beforeEach(() => {
    user.set(account(1));
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule], providers: [
      { provide: AuthService, useValue: { currentUser: user, getAccessToken: () => 'synthetic' } }
    ] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { http.verify(); TestBed.resetTestingModule(); });

  it('publishes asynchronous inbox changes inside Angular so an open chat renders without a click', fakeAsync(() => {
    const sync = TestBed.inject(InboxSyncService);
    const zone = TestBed.inject(NgZone);
    const zones: boolean[] = [];
    sync.changes$.subscribe(() => zones.push(NgZone.isInAngularZone()));
    TestBed.flushEffects(); tick(151);
    zone.runOutsideAngular(() => {
      http.expectOne(r => r.url.endsWith('/inbox/state')).flush(response('received', 1));
      flushMicrotasks();
    });
    expect(zones).toEqual([true]);
    TestBed.resetTestingModule();
  }));

  it('uses the authoritative aggregate and ignores a response from the previous school', fakeAsync(() => {
    const sync = TestBed.inject(InboxSyncService);
    TestBed.flushEffects(); tick(151);
    const old = http.expectOne(r => r.url.endsWith('/inbox/state'));
    user.set(account(2)); TestBed.flushEffects(); tick(151);
    const current = http.expectOne(r => r.url.endsWith('/inbox/state'));
    current.flush(response('school-2', 121)); flushMicrotasks();
    expect(sync.unreadMessages()).toBe(121);
    old.flush(response('school-1', 7)); flushMicrotasks();
    expect(sync.state()?.syncCursor).toBe('school-2');
    user.set(null); TestBed.flushEffects();
    expect(sync.unreadMessages()).toBe(0);
    TestBed.resetTestingModule();
  }));

  it('coalesces focus requests and explicitly reloads state when the cursor has expired', fakeAsync(() => {
    const sync = TestBed.inject(InboxSyncService);
    TestBed.flushEffects(); tick(151);
    http.expectOne(r => r.url.endsWith('/inbox/state')).flush(response('baseline'));
    flushMicrotasks();
    window.dispatchEvent(new Event('focus'));
    window.dispatchEvent(new Event('focus'));
    sync.refresh(); tick(151);
    http.expectOne(r => r.url.endsWith('/inbox/changes') && r.params.get('after') === 'baseline')
      .flush({ isSuccess: true, data: { items: [], nextCursor: 'expired', hasMore: false, resyncRequired: true } });
    flushMicrotasks();
    http.expectOne(r => r.url.endsWith('/inbox/state')).flush(response('recovered', 5)); flushMicrotasks();
    expect(sync.state()?.syncCursor).toBe('recovered');
    expect(sync.unreadMessages()).toBe(5);
    TestBed.resetTestingModule();
  }));
});
