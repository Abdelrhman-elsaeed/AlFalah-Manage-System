import { TestBed } from '@angular/core/testing';
import { NavigationStart, Router, provideRouter } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { Subject } from 'rxjs';
import { LandingHeaderComponent } from './landing-header.component';

describe('LandingHeaderComponent', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      imports: [LandingHeaderComponent, TranslateModule.forRoot()],
      providers: [provideRouter([])]
    })
  );

  it('closes the mobile menu on Escape and restores focus to the toggle', () => {
    const fixture = TestBed.createComponent(LandingHeaderComponent);
    fixture.detectChanges();
    const button = fixture.nativeElement.querySelector('.menu-toggle') as HTMLButtonElement;
    const focusSpy = spyOn(button, 'focus');
    fixture.componentInstance.menuOpen.set(true);
    fixture.componentInstance.onEscape();
    expect(fixture.componentInstance.menuOpen()).toBeFalse();
    expect(focusSpy).toHaveBeenCalled();
  });

  it('closes on navigation and unsubscribes on destruction', () => {
    const events = new Subject<NavigationStart>();
    spyOnProperty(TestBed.inject(Router), 'events', 'get').and.returnValue(events);
    const fixture = TestBed.createComponent(LandingHeaderComponent);
    fixture.componentInstance.menuOpen.set(true);
    events.next(new NavigationStart(1, '/auth/school-login'));
    expect(fixture.componentInstance.menuOpen()).toBeFalse();
    fixture.destroy();
    expect(events.observed).toBeFalse();
  });
});
