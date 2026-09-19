import { AfterViewInit, Component, DestroyRef, ElementRef, inject } from '@angular/core';
import { Meta } from '@angular/platform-browser';
import { ActivatedRoute } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { LandingHeaderComponent } from './components/landing-header/landing-header.component';
import { LandingHeroComponent } from './components/landing-hero/landing-hero.component';
import { VisionStripComponent } from './components/vision-strip/vision-strip.component';
import { AchievementsSectionComponent } from './components/achievements-section/achievements-section.component';
import { LandingFooterComponent } from './components/landing-footer/landing-footer.component';

@Component({
  selector: 'app-landing-page',
  standalone: true,
  imports: [
    TranslateModule,
    LandingHeaderComponent,
    LandingHeroComponent,
    VisionStripComponent,
    AchievementsSectionComponent,
    LandingFooterComponent
  ],
  templateUrl: './landing-page.component.html',
  styleUrls: ['./landing-page.component.css']
})
export class LandingPageComponent implements AfterViewInit {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly meta = inject(Meta);
  private readonly translate = inject(TranslateService);

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
  }

  ngAfterViewInit(): void {
    let frame = 0;
    this.route.fragment.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((fragment) => {
      cancelAnimationFrame(frame);
      if (fragment) frame = requestAnimationFrame(() => document.getElementById(fragment)?.scrollIntoView());
    });
    const observer = new IntersectionObserver(
      (entries) =>
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add('is-revealed');
            observer.unobserve(entry.target);
          }
        }),
      { threshold: 0.12 }
    );
    this.host.nativeElement.querySelectorAll('[data-reveal]').forEach((element) => observer.observe(element));
    this.destroyRef.onDestroy(() => {
      cancelAnimationFrame(frame);
      observer.disconnect();
    });
  }
}
