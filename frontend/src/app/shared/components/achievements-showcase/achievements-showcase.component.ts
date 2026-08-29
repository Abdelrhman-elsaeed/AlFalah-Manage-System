import { Component, OnInit, OnDestroy, signal, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface RibbonStat {
  id: string;
  targetNumber: number;
  prefix?: string;
  suffix?: string;
  label: string;
  icon: string;
  currentDisplay: number;
}

export interface AchievementStory {
  id: string;
  badge: string;
  badgeIcon: string;
  title: string;
  summary: string;
  fullText: string;
  coverImage: string;
  images: string[];
  tags: string[];
  dateStr?: string;
}

@Component({
  selector: 'app-achievements-showcase',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './achievements-showcase.component.html',
  styleUrls: ['./achievements-showcase.component.css']
})
export class AchievementsShowcaseComponent implements OnInit, OnDestroy {
  readonly activeStoryIndex = signal<number>(0);
  readonly selectedStory = signal<AchievementStory | null>(null);
  readonly currentModalImageIndex = signal<number>(0);
  readonly showAllHonorsModal = signal<boolean>(false);
  readonly isPaused = signal<boolean>(false);

  // 5 Key Stats with animated numbers
  readonly stats = signal<RibbonStat[]>([
    { id: 'ranks', targetNumber: 9, label: 'مراكز متقدمة', icon: 'pi-trophy', currentDisplay: 0 },
    { id: 'medals', targetNumber: 26, label: 'ميدالية منوعة', icon: 'pi-star-fill', currentDisplay: 0 },
    { id: 'projects', targetNumber: 19, label: 'مشروع إبداع', icon: 'pi-lightbulb', currentDisplay: 0 },
    { id: 'participations', targetNumber: 1970, prefix: '+', label: 'مشاركة طلابية', icon: 'pi-users', currentDisplay: 0 },
    { id: 'students', targetNumber: 317, label: 'طالباً بالمدرسة', icon: 'pi-graduation-cap', currentDisplay: 0 }
  ]);

  // Unique 3 main stories
  readonly stories: AchievementStory[] = [
    {
      id: 'story-1',
      badge: 'تكريم رسمي',
      badgeIcon: 'pi-verified',
      title: 'تكريم سعادة مدير عام التعليم بمكة لمتوسطة الفلاح لتفوقها بأولمبياد نسمو 2026',
      summary: 'لقاء جهودها المميزة في دعم ورعاية الطلاب وتحقيق مخرجات تنافسية وفوز المدرسة بأولمبياد نسمو الوطني.',
      fullText: 'سعادة مدير عام التعليم بمنطقة مكة المكرمة الأستاذ عبدالله الغنام يكرم متوسطة الفلاح بمكة المكرمة ؛ لقاء جهودها المميزة في دعم ورعاية الطلاب، والتي أثمرت عن تحقيق مخرجات تنافسية وفوز المدرسة في أولمبياد "نسمو" الوطني 2026.',
      coverImage: 'assets/achievements/1/TWMate.com-826be516744c4433f559426bb2649e6f.jpg',
      images: ['assets/achievements/1/TWMate.com-826be516744c4433f559426bb2649e6f.jpg'],
      tags: ['تعليم مكة', 'أولمبياد نسمو 2026', 'تكريم وتفوق'],
      dateStr: '1447 هـ'
    },
    {
      id: 'story-5',
      badge: 'أولمبياد نسمو • 4 صور',
      badgeIcon: 'pi-images',
      title: 'حفل تكريم فرسان الفلاح الفائزين بأولمبياد نسمو والمتأهلين لملتقى النخبة',
      summary: 'تكريم الطلاب الفائزين في مسابقة نسمو على مستوى المملكة، والمتأهلين لملتقى النخبة الوطني بكاوست.',
      fullText: 'تكريم الطلاب الفائزين في مسابقة نسمو على مستوى المملكة، والمتأهلين لملتقى النخبة بعد أداء استثنائي وتأهل للمنتخبات العلمية وحصد ميداليات متقدمة تعكس تميز مدارس الفلاح.',
      coverImage: 'assets/achievements/5/HKMiBPoXUAAfWqJ.jpg',
      images: [
        'assets/achievements/5/HKMiBPoXUAAfWqJ.jpg',
        'assets/achievements/5/HKMiBPpWAAAHXyx.jpg',
        'assets/achievements/5/HKMiBPrWAAAOUgl.jpg',
        'assets/achievements/5/HKMiBSRWwAAjhUq.jpg'
      ],
      tags: ['ملتقى النخبة', 'فرسان الفلاح', 'أبطال المملكة'],
      dateStr: '1447 هـ'
    },
    {
      id: 'story-3',
      badge: 'شراكة واعتماد',
      badgeIcon: 'pi-check-circle',
      title: 'توقيع اتفاقية شراكة استراتيجية مع هيئة تقويم التعليم والتدريب',
      summary: 'وقع سعادة الدكتور وجدي بن حامد بابطين اتفاقية تفاهم وشراكة تمهيداً لإجراءات الاعتماد المدرسي.',
      fullText: 'وقع سعادة الدكتور وجدي بن حامد بابطين، مدير عام الشؤون التعليمية والتطوير بمدارس الفلاح، اتفاقية تفاهم وشراكة مع هيئة تقويم التعليم والتدريب، وذلك تمهيدًا لإجراءات الاعتماد المدرسي وترسيخ معايير الجودة التعليمية.',
      coverImage: 'assets/achievements/3/HMN3sKGWsAA4sIx.jpg',
      images: ['assets/achievements/3/HMN3sKGWsAA4sIx.jpg'],
      tags: ['الاعتماد المدرسي', 'الشؤون التعليمية', 'الجودة'],
      dateStr: '1447 هـ'
    }
  ];

  private counterAnimationId?: number;
  private sliderTimerId?: any;

  ngOnInit(): void {
    this.animateCounters();
    this.startAutoSlider();
  }

  ngOnDestroy(): void {
    if (this.counterAnimationId) {
      cancelAnimationFrame(this.counterAnimationId);
    }
    this.stopAutoSlider();
  }

  private animateCounters(): void {
    const duration = 1600; // ms
    const startTime = performance.now();

    const step = (now: number) => {
      const elapsed = now - startTime;
      const progress = Math.min(elapsed / duration, 1);
      const easeOut = 1 - Math.pow(1 - progress, 3);

      this.stats.update(items =>
        items.map(item => ({
          ...item,
          currentDisplay: Math.round(item.targetNumber * easeOut)
        }))
      );

      if (progress < 1) {
        this.counterAnimationId = requestAnimationFrame(step);
      }
    };

    this.counterAnimationId = requestAnimationFrame(step);
  }

  startAutoSlider(): void {
    this.stopAutoSlider();
    this.sliderTimerId = setInterval(() => {
      if (!this.isPaused() && !this.selectedStory() && !this.showAllHonorsModal()) {
        this.nextStory();
      }
    }, 3500);
  }

  stopAutoSlider(): void {
    if (this.sliderTimerId) {
      clearInterval(this.sliderTimerId);
      this.sliderTimerId = null;
    }
  }

  pauseSlider(): void {
    this.isPaused.set(true);
  }

  resumeSlider(): void {
    this.isPaused.set(false);
  }

  nextStory(): void {
    this.activeStoryIndex.update(idx => (idx + 1) % this.stories.length);
  }

  prevStory(): void {
    this.activeStoryIndex.update(idx => (idx - 1 + this.stories.length) % this.stories.length);
  }

  goToStory(index: number): void {
    this.activeStoryIndex.set(index);
  }

  openStoryModal(story: AchievementStory): void {
    this.selectedStory.set(story);
    this.currentModalImageIndex.set(0);
  }

  closeStoryModal(): void {
    this.selectedStory.set(null);
  }

  setModalImageIndex(index: number): void {
    this.currentModalImageIndex.set(index);
  }

  nextModalImage(): void {
    const story = this.selectedStory();
    if (!story || story.images.length <= 1) return;
    this.currentModalImageIndex.update(idx => (idx + 1) % story.images.length);
  }

  prevModalImage(): void {
    const story = this.selectedStory();
    if (!story || story.images.length <= 1) return;
    this.currentModalImageIndex.update(idx => (idx - 1 + story.images.length) % story.images.length);
  }

  openAllHonors(): void {
    this.showAllHonorsModal.set(true);
  }

  closeAllHonors(): void {
    this.showAllHonorsModal.set(false);
  }

  @HostListener('document:keydown', ['$event'])
  handleKeyboardEvent(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      if (this.selectedStory()) this.closeStoryModal();
      if (this.showAllHonorsModal()) this.closeAllHonors();
    } else if (this.selectedStory()) {
      if (event.key === 'ArrowRight') this.prevModalImage();
      if (event.key === 'ArrowLeft') this.nextModalImage();
    } else {
      if (event.key === 'ArrowRight') this.prevStory();
      if (event.key === 'ArrowLeft') this.nextStory();
    }
  }
}
