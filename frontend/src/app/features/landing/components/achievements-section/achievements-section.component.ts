import { Component, ViewChild } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { AchievementsShowcaseComponent } from '../../../../shared/components/achievements-showcase/achievements-showcase.component';
import { ACHIEVEMENT_STORIES } from '../../../../shared/components/achievements-showcase/achievement-content';
import { AchievementFilmComponent } from '../achievement-film/achievement-film.component';
import { FalahQuillComponent } from '../falah-quill/falah-quill.component';
@Component({
  selector: 'app-achievements-section',
  standalone: true,
  imports: [TranslateModule, AchievementsShowcaseComponent, AchievementFilmComponent, FalahQuillComponent],
  template: `<section class="achievements-section" aria-labelledby="achievements-title">
    <div class="section-heading" data-reveal>
      <span class="eyebrow">{{ 'LANDING.ACHIEVEMENTS_EYEBROW' | translate }}</span>
      <h2 id="achievements-title">{{ 'LANDING.ACHIEVEMENTS_TITLE' | translate }}</h2>
      <app-falah-quill />
      <p>{{ 'LANDING.ACHIEVEMENTS_DESCRIPTION' | translate }}</p>
    </div>
    <app-achievement-film #film (honorsRequested)="showcase.openAllHonors()" (storyRequested)="openStory($event)" />
    <app-achievements-showcase #showcase class="achievement-details-source" view="honors" (dialogOpenChange)="film.setDialogOpen($event)" />
  </section>`,
  styleUrls: ['./achievements-section.component.css']
})
export class AchievementsSectionComponent {
  @ViewChild(AchievementsShowcaseComponent) private showcase?: AchievementsShowcaseComponent;
  openStory(id: string): void {
    const story = ACHIEVEMENT_STORIES.find(item => item.id === id);
    if (story) this.showcase?.openStoryModal(story);
  }
}
