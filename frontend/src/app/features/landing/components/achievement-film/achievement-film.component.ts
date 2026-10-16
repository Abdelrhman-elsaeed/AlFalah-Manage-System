import { AfterViewInit, Component, DestroyRef, ElementRef, EventEmitter, NgZone, Output, ViewChild, inject, signal } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { FalahQuillComponent } from '../falah-quill/falah-quill.component';
import { AchievementImageDirective } from '../../../../shared/components/achievements-showcase/achievement-image.directive';

interface FilmTimeline { time(value: number, suppressEvents?: boolean): FilmTimeline; kill(): void; }
interface FilmMotion { createTimeline(root: HTMLElement): Promise<{ timeline: FilmTimeline }>; }

@Component({
  selector: 'app-achievement-film',
  standalone: true,
  imports: [TranslateModule, FalahQuillComponent, AchievementImageDirective],
  templateUrl: './achievement-film.component.html',
  styleUrls: ['./achievement-film.component.css']
})
export class AchievementFilmComponent implements AfterViewInit {
  @Output() readonly honorsRequested = new EventEmitter<void>();
  @Output() readonly storyRequested = new EventEmitter<string>();
  @ViewChild('film', { static: true }) private film!: ElementRef<HTMLElement>;
  readonly active = signal(0);
  readonly playing = signal(false);
  readonly scenes = [
    { id: 'students', value: '317', label: 'students', title: 'لكل طالب… مساحة ليكبر.', copy: 'تكريم الطلاب الفائزين في مسابقة نسمو على مستوى المملكة، والمتأهلين لملتقى النخبة بعد أداء استثنائي وتأهل للمنتخبات العلمية.', details: ['317 طالباً بالمدرسة', 'تعليم نوعي ورعاية أقرب'], chapter: 'طلابنا', caption: 'عرض التغطية والصور: فرسان الفلاح', story: 'story-5', image: 'assets/achievements/5/HKMiBPoXUAAfWqJ.jpg' },
    { id: 'participations', value: '+1,970', label: 'participations', title: 'كل مشاركة… خطوة للأمام.', copy: 'فرص للمحاولة والاكتشاف وتجارب تفتح آفاق طلابنا داخل المدرسة وخارجها.', details: ['+1,970 مشاركة طلابية', 'مسابقات وفعاليات وتجارب'], chapter: 'المشاركات', caption: '', story: '', image: '' },
    { id: 'projects', value: '19', label: 'projects', title: 'أفكار تجد طريقها للنور.', copy: 'مشروعات إبداع تحمل فضول الطلاب وطموحهم، وتحول السؤال إلى تجربة قابلة للمشاركة.', details: ['19 مشروع إبداع', 'فضول يتحول إلى أثر'], chapter: 'الإبداع', caption: '', story: '', image: '' },
    { id: 'medals', value: '26', label: 'medals', title: 'للجهد… لحظة تتويج.', copy: 'تكريم الطلاب الفائزين في مسابقة نسمو على مستوى المملكة، والمتأهلين لملتقى النخبة بعد أداء استثنائي وتأهل للمنتخبات العلمية وحصد ميداليات متقدمة.', details: ['26 ميدالية منوعة', 'أولمبياد نسمو • تكريم الفائزين'], chapter: 'الميداليات', caption: 'عرض التغطية والصور: حفل التكريم', story: 'story-5', image: 'assets/achievements/5/HKMiBPpWAAAHXyx.jpg' },
    { id: 'ranks', value: '9', label: 'ranks', title: 'طموح يتقدّم… وأثر يبقى.', copy: 'سعادة مدير عام التعليم بمنطقة مكة يكرّم متوسطة الفلاح؛ لقاء جهودها المميزة في دعم ورعاية الطلاب وتحقيق مخرجات تنافسية وفوز المدرسة في أولمبياد نسمو الوطني.', details: ['9 مراكز متقدمة', 'دعم ورعاية ومخرجات تنافسية'], chapter: 'المراكز', caption: 'عرض التغطية والصور: تكريم تعليم مكة', story: 'story-1', image: 'assets/achievements/1/TWMate.com-826be516744c4433f559426bb2649e6f.jpg' }
  ];
  readonly dots = Array.from({ length: 36 }, (_, i) => {
    const ring = i < 12 ? 105 : 160, angle = (i < 12 ? i / 12 : (i - 12) / 24) * Math.PI * 2;
    return { x: 250 + Math.cos(angle) * ring, y: 210 + Math.sin(angle) * ring, gold: i % 4 === 0 };
  });
  private readonly zone = inject(NgZone);
  private readonly destroyRef = inject(DestroyRef);
  private readonly motion = matchMedia('(prefers-reduced-motion: reduce)');
  private timeline?: FilmTimeline;
  private frame = 0;
  private time = 0;
  private lastStamp?: number;
  private lastPaint = -1;
  private visible = false;
  private userPaused = false;
  private interactionPaused = false;
  private dialogPaused = false;
  private disposed = false;
  private loading?: Promise<void>;

  constructor() {
    this.destroyRef.onDestroy(() => {
      this.disposed = true;
      cancelAnimationFrame(this.frame);
      this.timeline?.kill();
    });
  }

  ngAfterViewInit(): void {
    this.zone.runOutsideAngular(() => {
      const root = this.film.nativeElement;
      const observer = new IntersectionObserver(entries => {
        this.visible = entries.some(entry => entry.isIntersecting);
        if (this.visible && !this.motion.matches) void this.load();
        this.sync();
      }, { threshold: .18 });
      observer.observe(root);
      const visibility = () => this.sync();
      const preference = () => {
        if (this.motion.matches) { this.userPaused = true; this.seek(0); }
        else { this.userPaused = false; if (this.visible) void this.load(); }
        this.sync();
      };
      document.addEventListener('visibilitychange', visibility);
      this.motion.addEventListener('change', preference);
      this.userPaused = this.motion.matches;
      this.destroyRef.onDestroy(() => {
        observer.disconnect();
        document.removeEventListener('visibilitychange', visibility);
        this.motion.removeEventListener('change', preference);
      });
      // The read-only controller also makes deterministic scene inspection possible.
      Object.defineProperty(root, 'achievementFilm', { value: {
        seek: (time: number) => this.inspect(time),
        get time() { return Number(root.dataset['filmTime'] ?? 0); },
        get playing() { return root.dataset['playing'] === 'true'; }
      } });
    });
  }

  private async load(): Promise<void> {
    this.loading ??= (async () => {
      try {
        const url = new URL('assets/motion/achievements/timeline.mjs?v=1', document.baseURI).href;
        const module: FilmMotion = await import(/* @vite-ignore */ url);
        const { timeline } = await module.createTimeline(this.film.nativeElement);
        if (this.disposed) { timeline.kill(); return; }
        this.timeline = timeline;
        this.paint();
        this.sync();
      } catch { this.userPaused = true; this.sync(); }
    })();
    return this.loading;
  }

  private paint(): void {
    this.timeline?.time(this.time, false);
    const index = Math.min(4, Math.floor(this.time / 9));
    if (index !== this.active()) this.zone.run(() => this.active.set(index));
    this.film.nativeElement.dataset['filmTime'] = String(this.time);
  }

  private readonly tick = (stamp: number) => {
    if (!this.playing() || this.disposed) return;
    if (this.lastStamp !== undefined) this.time = (this.time + Math.min((stamp - this.lastStamp) / 1000, .1)) % 45;
    this.lastStamp = stamp;
    if (this.time < this.lastPaint || this.time - this.lastPaint >= 1 / 30) {
      this.paint(); this.lastPaint = this.time;
    }
    this.frame = requestAnimationFrame(this.tick);
  };

  private sync(): void {
    const shouldPlay = !!this.timeline && this.visible && !document.hidden && !this.userPaused && !this.interactionPaused && !this.dialogPaused && !this.disposed;
    if (shouldPlay === this.playing()) return;
    this.zone.run(() => this.playing.set(shouldPlay));
    this.film.nativeElement.dataset['playing'] = String(shouldPlay);
    this.lastStamp = undefined;
    cancelAnimationFrame(this.frame);
    if (shouldPlay) this.frame = requestAnimationFrame(this.tick);
  }

  toggle(): void {
    this.userPaused = !this.userPaused;
    this.interactionPaused = false;
    if (!this.userPaused) void this.load();
    this.zone.runOutsideAngular(() => this.sync());
  }

  choose(index: number): void {
    this.seek(index * 9);
    if (!this.motion.matches) void this.load();
  }

  private seek(time: number): void {
    this.time = Math.max(0, Math.min(44.999, time));
    this.lastStamp = undefined;
    this.lastPaint = -1;
    this.paint();
  }

  private async inspect(time: number): Promise<void> {
    this.userPaused = true;
    await this.load();
    this.seek(time);
    this.sync();
  }

  onFocus(): void { this.interactionPaused = true; this.sync(); }
  onBlur(event: FocusEvent): void {
    this.interactionPaused = event.relatedTarget instanceof Node && this.film.nativeElement.contains(event.relatedTarget);
    this.sync();
  }
  setDialogOpen(open: boolean): void { this.dialogPaused = open; this.sync(); }
  openStory(id: string): void { this.storyRequested.emit(id); }
}
