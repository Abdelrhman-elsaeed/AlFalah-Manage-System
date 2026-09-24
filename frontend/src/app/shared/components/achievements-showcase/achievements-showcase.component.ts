import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  ViewChild,
  computed,
  inject,
  input,
  signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { LucideDynamicIcon } from '@lucide/angular';
import { ACHIEVEMENT_STATS, ACHIEVEMENT_STORIES } from './achievement-content';
import { AchievementStory } from './achievement.models';
import { AchievementImageDirective } from './achievement-image.directive';

@Component({
  selector: 'app-achievements-showcase',
  standalone: true,
  imports: [CommonModule, TranslateModule, LucideDynamicIcon, AchievementImageDirective],
  templateUrl: './achievements-showcase.component.html',
  styleUrls: ['./achievements-showcase.component.css']
})
export class AchievementsShowcaseComponent implements AfterViewInit, OnDestroy {
  readonly view = input<'all' | 'achievements' | 'news'>('all');
  readonly activeStoryIndex = signal(1);
  readonly selectedStory = signal<AchievementStory | null>(null);
  readonly currentModalImageIndex = signal(0);
  readonly showAllHonorsModal = signal(false);
  readonly manuallyPaused = signal(false);
  readonly reducedMotion = signal(matchMedia('(prefers-reduced-motion: reduce)').matches);
  readonly isPaused = signal(true);
  readonly stats = signal(
    ACHIEVEMENT_STATS.map((stat) => ({
      ...stat,
      currentDisplay: this.reducedMotion() ? stat.targetNumber : 0
    }))
  );
  readonly stories = ACHIEVEMENT_STORIES;
  readonly honors = Array.from({ length: 12 }, (_, i) => String(i));
  readonly activeStory = computed(() => this.stories[this.activeStoryIndex()]);
  readonly storyTrackTransform = computed(() => {
    const index = this.activeStoryIndex();
    return index === 0 ? 'translateX(0)' : `translateX(calc(${-100 * index}% - ${18 * index}px))`;
  });
  isPreviousStory(index: number): boolean {
    return index === (this.activeStoryIndex() - 1 + this.stories.length) % this.stories.length;
  }
  isNextStory(index: number): boolean {
    return index === (this.activeStoryIndex() + 1) % this.stories.length;
  }
  @ViewChild('detailsDialog', { static: true }) detailsDialog!: ElementRef<HTMLDialogElement>;
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly changeDetector = inject(ChangeDetectorRef);
  private readonly motion = matchMedia('(prefers-reduced-motion: reduce)');
  private readonly onMotionChange = () => {
    this.reducedMotion.set(this.motion.matches);
    if (this.motion.matches) this.finishCounters();
    this.syncPlayback();
  };
  private observer?: IntersectionObserver;
  private counterAnimationId?: number;
  private sliderTimerId?: ReturnType<typeof setInterval>;
  private hasAnimated = false;
  private visible = false;
  private hovered = false;
  private focused = false;
  private opener?: HTMLElement;
  private previousOverflow?: string;

  ngAfterViewInit(): void {
    this.reducedMotion.set(this.motion.matches);
    this.motion.addEventListener('change', this.onMotionChange);
    this.observer = new IntersectionObserver(
      (entries) => {
        this.visible = entries.some((entry) => entry.isIntersecting);
        if (this.visible && !this.hasAnimated && this.view() !== 'news') {
          this.hasAnimated = true;
          if (!this.reducedMotion()) this.animateCounters();
        }
        this.syncPlayback();
      },
      { threshold: 0.15 }
    );
    this.observer.observe(this.host.nativeElement);
  }
  ngOnDestroy(): void {
    this.stopAutoSlider();
    if (this.counterAnimationId !== undefined) cancelAnimationFrame(this.counterAnimationId);
    this.observer?.disconnect();
    this.motion.removeEventListener('change', this.onMotionChange);
    this.restoreScrolling();
  }
  private animateCounters(): void {
    const start = performance.now();
    const step = (now: number) => {
      const progress = Math.min((now - start) / 1200, 1);
      this.stats.update((items) =>
        items.map((item) => ({
          ...item,
          currentDisplay: Math.round(item.targetNumber * (1 - (1 - progress) ** 3))
        }))
      );
      if (progress < 1) this.counterAnimationId = requestAnimationFrame(step);
    };
    this.counterAnimationId = requestAnimationFrame(step);
  }
  private finishCounters(): void {
    if (this.counterAnimationId !== undefined) cancelAnimationFrame(this.counterAnimationId);
    this.stats.update((items) => items.map((item) => ({ ...item, currentDisplay: item.targetNumber })));
  }
  private syncPlayback(): void {
    const paused =
      this.view() === 'achievements' ||
      !this.visible ||
      this.hovered ||
      this.focused ||
      this.manuallyPaused() ||
      this.reducedMotion() ||
      document.hidden ||
      !!this.selectedStory() ||
      this.showAllHonorsModal();
    this.isPaused.set(paused);
    this.stopAutoSlider();
    if (!paused) this.sliderTimerId = setInterval(() => this.nextStory(), 6500);
  }
  stopAutoSlider(): void {
    if (this.sliderTimerId !== undefined) clearInterval(this.sliderTimerId);
    this.sliderTimerId = undefined;
  }
  setHovered(value: boolean): void {
    this.hovered = value;
    this.syncPlayback();
  }
  onFocusIn(): void {
    this.focused = true;
    this.syncPlayback();
  }
  onFocusOut(event: FocusEvent): void {
    this.focused =
      event.relatedTarget instanceof Node && this.host.nativeElement.contains(event.relatedTarget);
    this.syncPlayback();
  }
  togglePlayback(): void {
    this.manuallyPaused.update((value) => !value);
    this.syncPlayback();
  }
  @HostListener('document:visibilitychange') onVisibilityChange(): void {
    this.syncPlayback();
  }
  nextStory(): void {
    this.activeStoryIndex.update((index) => (index + 1) % this.stories.length);
  }
  prevStory(): void {
    this.activeStoryIndex.update((index) => (index - 1 + this.stories.length) % this.stories.length);
  }
  goToStory(index: number): void {
    this.activeStoryIndex.set(index);
  }
  onSliderKey(event: KeyboardEvent): void {
    if (event.key === 'ArrowRight') {
      event.preventDefault();
      this.prevStory();
    }
    if (event.key === 'ArrowLeft') {
      event.preventDefault();
      this.nextStory();
    }
  }
  openStoryModal(story: AchievementStory): void {
    this.selectedStory.set(story);
    this.currentModalImageIndex.set(0);
    this.openDialog();
  }
  openAllHonors(): void {
    this.showAllHonorsModal.set(true);
    this.openDialog();
  }
  private openDialog(): void {
    this.opener = document.activeElement instanceof HTMLElement ? document.activeElement : undefined;
    this.previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    // Render the selected story and accessible name before the browser moves focus.
    this.changeDetector.detectChanges();
    this.detailsDialog.nativeElement.showModal();
    this.syncPlayback();
  }
  closeDialog(): void {
    this.detailsDialog.nativeElement.close();
  }
  onDialogClosed(): void {
    this.selectedStory.set(null);
    this.showAllHonorsModal.set(false);
    this.restoreScrolling();
    if (this.opener?.isConnected) this.opener.focus({ preventScroll: true });
    this.syncPlayback();
  }
  private restoreScrolling(): void {
    if (this.previousOverflow !== undefined) {
      document.body.style.overflow = this.previousOverflow;
      this.previousOverflow = undefined;
    }
  }
  onBackdropClick(event: MouseEvent): void {
    if (event.target !== this.detailsDialog.nativeElement) return;
    const rect = this.detailsDialog.nativeElement.getBoundingClientRect();
    if (
      event.clientX < rect.x ||
      event.clientX > rect.right ||
      event.clientY < rect.y ||
      event.clientY > rect.bottom
    )
      this.closeDialog();
  }
  setModalImageIndex(index: number): void {
    this.currentModalImageIndex.set(index);
  }
  nextModalImage(): void {
    const story = this.selectedStory();
    if (story) this.currentModalImageIndex.update((index) => (index + 1) % story.images.length);
  }
  prevModalImage(): void {
    const story = this.selectedStory();
    if (story)
      this.currentModalImageIndex.update((index) => (index - 1 + story.images.length) % story.images.length);
  }
  onGalleryKey(event: KeyboardEvent): void {
    if (event.key === 'ArrowRight') {
      event.preventDefault();
      this.prevModalImage();
    }
    if (event.key === 'ArrowLeft') {
      event.preventDefault();
      this.nextModalImage();
    }
  }
}
