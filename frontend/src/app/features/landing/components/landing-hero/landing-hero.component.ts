import { AfterViewInit, Component, ElementRef, NgZone, OnDestroy, ViewChild, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';

interface NetworkInformationLike {
  effectiveType?: string;
  saveData?: boolean;
}

interface NavigatorWithConnection extends Navigator {
  connection?: NetworkInformationLike;
}

const HERO_VIDEO_MP4 = 'assets/media/hero.mp4';
const HERO_VIDEO_WEBM = 'assets/media/hero.webm';

@Component({
  selector: 'app-landing-hero',
  standalone: true,
  imports: [RouterLink, TranslateModule],
  templateUrl: './landing-hero.component.html',
  styleUrls: ['./landing-hero.component.css']
})
export class LandingHeroComponent implements AfterViewInit, OnDestroy {
  @ViewChild('heroVideo') private heroVideo?: ElementRef<HTMLVideoElement>;

  readonly videoReady = signal(false);

  private idleCallbackId?: number;
  private fallbackTimerId?: number;

  constructor(private readonly ngZone: NgZone) {}

  ngAfterViewInit(): void {
    this.ngZone.runOutsideAngular(() => {
      if (!this.shouldLoadVideo()) {
        return;
      }

      document.addEventListener('visibilitychange', this.handleVisibilityChange);

      if (document.readyState === 'complete') {
        this.scheduleVideoLoad();
        return;
      }

      window.addEventListener('load', this.handleWindowLoad, { once: true });
    });
  }

  ngOnDestroy(): void {
    window.removeEventListener('load', this.handleWindowLoad);
    document.removeEventListener('visibilitychange', this.handleVisibilityChange);

    if (this.idleCallbackId !== undefined) {
      window.cancelIdleCallback?.(this.idleCallbackId);
    }
    if (this.fallbackTimerId !== undefined) {
      window.clearTimeout(this.fallbackTimerId);
    }
  }

  onVideoPlaying(): void {
    this.videoReady.set(true);
  }

  onVideoError(): void {
    this.videoReady.set(false);
  }

  explore(event: MouseEvent): void {
    event.preventDefault();
    const target = document.getElementById('achievements');
    target?.setAttribute('tabindex', '-1');
    target?.focus({ preventScroll: true });
    target?.scrollIntoView({
      behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth'
    });
    history.replaceState(history.state, '', '#achievements');
  }

  private readonly handleWindowLoad = (): void => {
    this.scheduleVideoLoad();
  };

  private readonly handleVisibilityChange = (): void => {
    const video = this.heroVideo?.nativeElement;
    if (!video) {
      return;
    }

    if (document.hidden) {
      video.pause();
      return;
    }

    if (!video.src) {
      this.scheduleVideoLoad();
      return;
    }

    void video.play().catch(() => this.videoReady.set(false));
  };

  private shouldLoadVideo(): boolean {
    if (matchMedia('(prefers-reduced-motion: reduce)').matches) {
      return false;
    }

    const connection = (navigator as NavigatorWithConnection).connection;
    return !connection?.saveData && connection?.effectiveType !== 'slow-2g' && connection?.effectiveType !== '2g';
  }

  private scheduleVideoLoad(): void {
    if (window.requestIdleCallback) {
      this.idleCallbackId = window.requestIdleCallback(() => this.loadVideo(), { timeout: 1800 });
      return;
    }

    this.fallbackTimerId = window.setTimeout(() => this.loadVideo(), 700);
  }

  private loadVideo(): void {
    const video = this.heroVideo?.nativeElement;
    if (!video || video.src || document.hidden) {
      return;
    }

    video.muted = true;
    video.src = video.canPlayType('video/webm; codecs="vp9"') ? HERO_VIDEO_WEBM : HERO_VIDEO_MP4;
    video.load();
    void video.play().catch(() => this.videoReady.set(false));
  }
}
