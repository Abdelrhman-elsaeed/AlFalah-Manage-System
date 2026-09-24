import { Component } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
@Component({
  selector: 'app-vision-strip',
  standalone: true,
  imports: [TranslateModule],
  template: `<section class="vision" aria-labelledby="vision-title">
    <div class="vision-inner">
      <div class="vision-intro" data-reveal>
        <span>{{ 'LANDING.NAV.vision' | translate }}</span>
        <h2 id="vision-title">{{ 'LANDING.VISION_TITLE' | translate }}</h2>
      </div>
      @for (pillar of pillars; track pillar.key) {
        <article class="pillar" data-reveal [style.--reveal-delay]="$index * 110 + 'ms'">
          <span class="pillar-icon"><i [class]="'pi ' + pillar.icon" aria-hidden="true"></i></span>
          <div>
            <h3>{{ 'LANDING.PILLARS.' + pillar.key | translate }}</h3>
            <p>{{ 'LANDING.PILLAR_DESCRIPTIONS.' + pillar.key | translate }}</p>
          </div>
        </article>
      }
    </div>
  </section>`,
  styleUrls: ['./vision-strip.component.css']
})
export class VisionStripComponent {
  readonly pillars = [
    { key: 'VALUES', icon: 'pi-heart' },
    { key: 'EDUCATION', icon: 'pi-book' },
    { key: 'AMBITION', icon: 'pi-flag' }
  ];
}
