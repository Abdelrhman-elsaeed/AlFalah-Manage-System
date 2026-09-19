import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { AchievementsShowcaseComponent } from './achievements-showcase.component';

describe('AchievementsShowcaseComponent', () => {
  let fixture: ComponentFixture<AchievementsShowcaseComponent>;
  let component: AchievementsShowcaseComponent;
  let observe: IntersectionObserverCallback;
  let changeMotion: () => void;
  let disconnect: jasmine.Spy;
  let motion: MediaQueryList;

  beforeEach(async () => {
    disconnect = jasmine.createSpy('disconnect');
    spyOn(window, 'IntersectionObserver').and.callFake(function (callback: IntersectionObserverCallback) {
      observe = callback;
      return { observe() {}, disconnect, unobserve() {} } as unknown as IntersectionObserver;
    });
    motion = {
      matches: false,
      addEventListener: (_: string, callback: () => void) => (changeMotion = callback),
      removeEventListener: jasmine.createSpy('removeEventListener')
    } as unknown as MediaQueryList;
    spyOn(window, 'matchMedia').and.returnValue(motion);
    await TestBed.configureTestingModule({
      imports: [AchievementsShowcaseComponent, TranslateModule.forRoot()]
    }).compileComponents();
    fixture = TestBed.createComponent(AchievementsShowcaseComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  function visible(value: boolean): void {
    observe([{ isIntersecting: value } as IntersectionObserverEntry], {} as IntersectionObserver);
  }

  it('starts all five approved counters at zero and renders the three news cards', () => {
    expect(component.stats().map((stat) => stat.targetNumber)).toEqual([9, 26, 19, 1970, 317]);
    expect(component.stats().every((stat) => stat.currentDisplay === 0)).toBeTrue();
    expect(fixture.nativeElement.querySelectorAll('.story-card').length).toBe(3);
    component.prevStory();
    expect(component.activeStoryIndex()).toBe(2);
    component.nextStory();
    expect(component.activeStoryIndex()).toBe(0);
    component.goToStory(1);
    expect(component.activeStory().images.length).toBe(4);
  });

  it('runs only while visible and pauses independently for hover and focus', fakeAsync(() => {
    tick(6500);
    expect(component.activeStoryIndex()).toBe(0);
    visible(true);
    tick(6500);
    expect(component.activeStoryIndex()).toBe(1);
    component.setHovered(true);
    component.onFocusIn();
    component.setHovered(false);
    tick(6500);
    expect(component.activeStoryIndex()).toBe(1);
    component.onFocusOut(new FocusEvent('focusout'));
    tick(6500);
    expect(component.activeStoryIndex()).toBe(2);
    visible(false);
    tick(6500);
    expect(component.activeStoryIndex()).toBe(2);
    fixture.destroy();
  }));

  it('stops autoplay on a live reduced-motion change and releases observers on destroy', fakeAsync(() => {
    visible(true);
    Object.defineProperty(motion, 'matches', { value: true });
    changeMotion();
    tick(14000);
    expect(component.activeStoryIndex()).toBe(0);
    expect(component.stats().every((stat) => stat.currentDisplay === stat.targetNumber)).toBeTrue();
    fixture.destroy();
    expect(disconnect).toHaveBeenCalled();
    expect(motion.removeEventListener).toHaveBeenCalled();
  }));

  it('keeps manual pause when pointer and focus leave', fakeAsync(() => {
    visible(true);
    component.togglePlayback();
    component.setHovered(false);
    component.onFocusOut(new FocusEvent('focusout'));
    tick(14000);
    expect(component.activeStoryIndex()).toBe(0);
    component.togglePlayback();
    tick(6500);
    expect(component.activeStoryIndex()).toBe(1);
    fixture.destroy();
  }));

  it('opens a named modal, pauses playback, restores focus and body scrolling', fakeAsync(() => {
    const opener = fixture.nativeElement.querySelector('.stat-card') as HTMLButtonElement;
    opener.focus();
    component.openAllHonors();
    fixture.detectChanges();
    const dialog = component.detailsDialog.nativeElement;
    expect(dialog.open).toBeTrue();
    expect(dialog.getAttribute('aria-labelledby')).toBe('achievement-dialog-title');
    expect(document.body.style.overflow).toBe('hidden');
    expect(component.isPaused()).toBeTrue();
    component.closeDialog();
    tick();
    // Native close events are dispatched on a browser task; exercise their handler deterministically.
    component.onDialogClosed();
    expect(component.showAllHonorsModal()).toBeFalse();
    expect(document.activeElement).toBe(opener);
    expect(document.body.style.overflow).not.toBe('hidden');
  }));

  it('uses native buttons so Space and Enter activate every statistic and story', () => {
    expect(fixture.nativeElement.querySelectorAll('button.stat-card').length).toBe(5);
    expect(fixture.nativeElement.querySelector('button.story-image')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('button.read-story')).not.toBeNull();
  });
});
