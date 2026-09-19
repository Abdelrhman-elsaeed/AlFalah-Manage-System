import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { LandingHeroComponent } from './landing-hero.component';

describe('LandingHeroComponent', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      imports: [LandingHeroComponent, TranslateModule.forRoot()],
      providers: [provideRouter([])]
    })
  );

  it('renders the poster without an eager video request', () => {
    spyOn(window, 'matchMedia').and.returnValue({ matches: false } as MediaQueryList);
    const idleSpy = spyOn(window, 'requestIdleCallback').and.returnValue(1);
    const fixture = TestBed.createComponent(LandingHeroComponent);

    fixture.detectChanges();
    window.dispatchEvent(new Event('load'));

    const video = fixture.nativeElement.querySelector('video') as HTMLVideoElement;
    expect(video.getAttribute('src')).toBeNull();
    expect(video.querySelector('source')).toBeNull();
    expect(video.preload).toBe('none');
    expect(idleSpy).toHaveBeenCalled();
    fixture.destroy();
  });

  it('does not request the video when reduced motion is enabled', () => {
    spyOn(window, 'matchMedia').and.returnValue({ matches: true } as MediaQueryList);
    const idleSpy = spyOn(window, 'requestIdleCallback').and.returnValue(1);
    const fixture = TestBed.createComponent(LandingHeroComponent);

    fixture.detectChanges();
    window.dispatchEvent(new Event('load'));

    const video = fixture.nativeElement.querySelector('video') as HTMLVideoElement;
    expect(video.getAttribute('src')).toBeNull();
    expect(idleSpy).not.toHaveBeenCalled();
    fixture.destroy();
  });

  it('adds an optimized source only from the deferred callback', () => {
    spyOn(window, 'matchMedia').and.returnValue({ matches: false } as MediaQueryList);
    let deferredLoad: (() => void) | undefined;
    spyOn(window, 'requestIdleCallback').and.callFake(callback => {
      deferredLoad = () => callback({ didTimeout: false, timeRemaining: () => 20 });
      return 1;
    });
    spyOn(HTMLMediaElement.prototype, 'load');
    spyOn(HTMLMediaElement.prototype, 'play').and.returnValue(Promise.resolve());
    const fixture = TestBed.createComponent(LandingHeroComponent);

    fixture.detectChanges();
    window.dispatchEvent(new Event('load'));
    const video = fixture.nativeElement.querySelector('video') as HTMLVideoElement;
    expect(video.getAttribute('src')).toBeNull();

    deferredLoad?.();

    expect(video.src).toMatch(/assets\/media\/hero\.(webm|mp4)$/);
    expect(video.load).toHaveBeenCalled();
    expect(video.play).toHaveBeenCalled();
    fixture.destroy();
  });
});
