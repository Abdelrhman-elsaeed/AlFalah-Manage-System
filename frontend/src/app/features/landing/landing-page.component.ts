import { AfterViewInit, Component, DestroyRef, ElementRef, inject, signal } from '@angular/core';
import { Meta } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { LandingHeaderComponent } from './components/landing-header/landing-header.component';
import { LandingHeroComponent } from './components/landing-hero/landing-hero.component';
import { VisionStripComponent } from './components/vision-strip/vision-strip.component';
import { AchievementsSectionComponent } from './components/achievements-section/achievements-section.component';
import { OrganizationSectionComponent } from './components/organization-section/organization-section.component';
import { NewsSectionComponent } from './components/news-section/news-section.component';
import { LandingFooterComponent } from './components/landing-footer/landing-footer.component';

@Component({
  selector: 'app-landing-page',
  standalone: true,
  imports: [
    TranslateModule,
    LandingHeaderComponent,
    LandingHeroComponent,
    VisionStripComponent,
    OrganizationSectionComponent,
    AchievementsSectionComponent,
    NewsSectionComponent,
    LandingFooterComponent
  ],
  templateUrl: './landing-page.component.html',
  styleUrls: ['./landing-page.component.css']
})
export class LandingPageComponent implements AfterViewInit {
  readonly preloaderState = signal<'visible' | 'leaving' | 'hidden'>('visible');
  readonly loginNavigationPending = signal(false);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly meta = inject(Meta);
  private readonly translate = inject(TranslateService);
  private readonly timers: number[] = [];

  constructor() {
    const previous = this.meta.getTag('name="description"')?.content;
    this.translate
      .stream('LANDING.DESCRIPTION')
      .pipe(takeUntilDestroyed())
      .subscribe((content) => {
        this.meta.updateTag({ name: 'description', content });
      });
    this.destroyRef.onDestroy(() =>
      previous === undefined
        ? this.meta.removeTag('name="description"')
        : this.meta.updateTag({ name: 'description', content: previous })
    );

    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    this.timers.push(
      window.setTimeout(() => this.preloaderState.set('leaving'), reducedMotion ? 80 : 520),
      window.setTimeout(() => this.preloaderState.set('hidden'), reducedMotion ? 140 : 820)
    );
    this.destroyRef.onDestroy(() => this.timers.forEach((timer) => window.clearTimeout(timer)));
  }

  ngAfterViewInit(): void {
    let frame = 0;
    this.route.fragment.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((fragment) => {
      cancelAnimationFrame(frame);
      if (fragment) {
        const target = document.getElementById(fragment);
        target?.classList.add('is-revealed');
        target?.querySelectorAll('[data-reveal]').forEach((element) => element.classList.add('is-revealed'));
        frame = requestAnimationFrame(() => target?.scrollIntoView());
      }
    });
    const revealElements = this.host.nativeElement.querySelectorAll<HTMLElement>('[data-reveal]');
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      revealElements.forEach((element) => element.classList.add('is-revealed'));
      return;
    }

    const observer = new IntersectionObserver(
      (entries) =>
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add('is-revealed');
            observer.unobserve(entry.target);
          }
        }),
      { threshold: 0.12, rootMargin: '0px 0px -7% 0px' }
    );
    revealElements.forEach((element) => observer.observe(element));
    this.timers.push(
      window.setTimeout(
        () => revealElements.forEach((element) => element.classList.add('is-revealed')),
        1800
      )
    );
    this.destroyRef.onDestroy(() => {
      cancelAnimationFrame(frame);
      observer.disconnect();
    });
  }

  openLogin(): void {
    if (this.loginNavigationPending()) return;

    this.loginNavigationPending.set(true);
    this.preloaderState.set('visible');
    const delay = window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 0 : 420;
    this.timers.push(
      window.setTimeout(() => {
        void this.router.navigateByUrl('/auth/school-login').catch(() => {
          this.loginNavigationPending.set(false);
          this.preloaderState.set('hidden');
        });
      }, delay)
    );
  }
}
