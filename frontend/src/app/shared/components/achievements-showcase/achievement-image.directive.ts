import { Directive, ElementRef, HostListener, Input, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

@Directive({ selector: 'img[achievementImage]', standalone: true })
export class AchievementImageDirective {
  private readonly element = inject<ElementRef<HTMLImageElement>>(ElementRef);
  private readonly translate = inject(TranslateService);
  private originalAlt?: string;
  @Input() set achievementImage(source: string) {
    if (this.originalAlt !== undefined) {
      this.element.nativeElement.alt = this.originalAlt;
      this.originalAlt = undefined;
    }
    this.element.nativeElement.classList.remove('image-fallback');
    this.element.nativeElement.src = source;
  }
  @HostListener('error') onError(): void {
    const image = this.element.nativeElement;
    if (image.classList.contains('image-fallback')) return;
    image.classList.add('image-fallback');
    this.originalAlt = image.alt;
    image.alt = `${this.translate.instant('LANDING.IMAGE_UNAVAILABLE')} — ${this.translate.instant('LANDING.LOGO_ALT')}`;
    image.src = 'assets/Logo.png';
  }
}
