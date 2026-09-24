import { Component } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { AchievementsShowcaseComponent } from '../../../../shared/components/achievements-showcase/achievements-showcase.component';
@Component({
  selector: 'app-achievements-section',
  standalone: true,
  imports: [TranslateModule, AchievementsShowcaseComponent],
  template: `<section class="achievements-section" aria-labelledby="achievements-title">
    <div class="section-heading" data-reveal>
      <span class="eyebrow">{{ 'LANDING.ACHIEVEMENTS_EYEBROW' | translate }}</span>
      <h2 id="achievements-title">{{ 'LANDING.ACHIEVEMENTS_TITLE' | translate }}</h2>
      <p>{{ 'LANDING.ACHIEVEMENTS_DESCRIPTION' | translate }}</p>
    </div>
    <app-achievements-showcase view="achievements" data-reveal style="--reveal-delay: 120ms" />
  </section>`,
  styleUrls: ['./achievements-section.component.css']
})
export class AchievementsSectionComponent {}
