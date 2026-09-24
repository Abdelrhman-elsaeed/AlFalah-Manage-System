import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { LandingPageComponent } from './landing-page.component';

describe('LandingPageComponent visual structure', () => {
  beforeEach(() => {
    const reducedMotionQuery = {
      matches: true,
      addEventListener: () => undefined,
      removeEventListener: () => undefined
    } as unknown as MediaQueryList;
    spyOn(window, 'matchMedia').and.returnValue(reducedMotionQuery);

    return TestBed.configureTestingModule({
      imports: [LandingPageComponent, TranslateModule.forRoot()],
      providers: [provideRouter([])]
    });
  });

  it('renders the preloader logo in a circular frame without stretching it', () => {
    const fixture = TestBed.createComponent(LandingPageComponent);
    fixture.detectChanges();

    const logo = fixture.nativeElement.querySelector('.preloader-mark img') as HTMLImageElement;
    const mark = fixture.nativeElement.querySelector('.preloader-mark') as HTMLElement;

    expect(logo.clientWidth).toBe(logo.clientHeight);
    expect(mark.clientWidth).toBe(mark.clientHeight);
    expect(getComputedStyle(mark).borderRadius).toBe('50%');
    expect(getComputedStyle(logo).objectFit).toBe('contain');
    fixture.destroy();
  });

  it('renders achievements and news as separate consecutive sections', () => {
    const fixture = TestBed.createComponent(LandingPageComponent);
    fixture.detectChanges();

    const achievements = fixture.nativeElement.querySelector('#achievements') as HTMLElement;
    const news = fixture.nativeElement.querySelector('#news') as HTMLElement;

    expect(achievements).withContext('achievements section should exist').not.toBeNull();
    expect(news).withContext('news section should exist').not.toBeNull();
    expect(achievements.nextElementSibling).toBe(news);
    expect(achievements.querySelector('.stats-panel')).not.toBeNull();
    expect(achievements.querySelector('.story-carousel')).toBeNull();
    expect(news.querySelector('.stats-panel')).toBeNull();
    expect(news.querySelector('.story-carousel')).not.toBeNull();
    fixture.destroy();
  });

  it('shows the preloader while navigating from the login button', fakeAsync(() => {
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigateByUrl').and.returnValue(Promise.resolve(true));
    const fixture = TestBed.createComponent(LandingPageComponent);
    fixture.detectChanges();
    tick(200);
    fixture.detectChanges();

    const login = fixture.nativeElement.querySelector('app-landing-header .login') as HTMLAnchorElement;
    login.click();
    fixture.detectChanges();

    expect(fixture.componentInstance.preloaderState()).toBe('visible');
    tick(1);
    expect(navigateSpy).toHaveBeenCalled();
    fixture.destroy();
  }));

  it('keeps news autoplay enabled without exposing a playback button', () => {
    const fixture = TestBed.createComponent(LandingPageComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#news .playback')).toBeNull();
    fixture.destroy();
  });
});
