import { Component } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { AchievementsShowcaseComponent } from '../../../../shared/components/achievements-showcase/achievements-showcase.component';

@Component({
  selector: 'app-news-section',
  standalone: true,
  imports: [TranslateModule, AchievementsShowcaseComponent],
  templateUrl: './news-section.component.html',
  styleUrls: ['./news-section.component.css']
})
export class NewsSectionComponent {}
