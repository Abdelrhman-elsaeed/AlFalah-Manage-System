import { Component } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { AchievementsShowcaseComponent } from '../../../../shared/components/achievements-showcase/achievements-showcase.component';
@Component({
  selector: 'app-achievements-section',
  standalone: true,
  imports: [TranslateModule, AchievementsShowcaseComponent],
  template: `<section class="achievements-section" aria-labelledby="achievements-title">
    <div class="section-heading">
      <span class="eyebrow">{{ 'LANDING.ACHIEVEMENTS_EYEBROW' | translate }}</span>
      <h2 id="achievements-title">{{ 'LANDING.ACHIEVEMENTS_TITLE' | translate }}</h2>
      <p>{{ 'LANDING.ACHIEVEMENTS_DESCRIPTION' | translate }}</p>
    </div>
    <app-achievements-showcase />
  </section>`,
  styles: [
    ':host{display:block}.achievements-section{width:var(--landing-container);margin-inline:auto;padding-block:clamp(84px,9vw,116px) clamp(92px,10vw,128px)}.section-heading{text-align:center;max-width:760px;margin-inline:auto;margin-block-end:48px}.eyebrow{color:var(--gold-700);font-weight:700;font-size:.9rem}h2{color:var(--brand-950);font-size:clamp(1.7rem,3vw,2.6rem);font-weight:800;margin-block:14px}p{color:var(--text-muted);font-size:1.05rem;line-height:1.8;margin:0}@media(max-width:768px){.achievements-section{padding-block:64px 80px}.section-heading{margin-block-end:34px}}'
  ]
})
export class AchievementsSectionComponent {}
