import { AfterViewInit, Component, DestroyRef, ElementRef, NgZone, ViewChild, inject } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { FALAH_STORY_MODULE } from './story-assets';

interface StoryController { dispose(): void; }
interface StoryModule {
  mountSection(container: HTMLElement, options: {
    heightReference: HTMLElement;
    wrapper: HTMLElement;
    isCancelled: () => boolean;
  }): Promise<StoryController>;
}

@Component({
  selector: 'app-system-story',
  standalone: true,
  imports: [TranslateModule],
  templateUrl: './system-story.component.html',
  styleUrls: ['./system-story.component.css']
})
export class SystemStoryComponent implements AfterViewInit {
  @ViewChild('stage', { static: true }) private stage!: ElementRef<HTMLElement>;
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly zone = inject(NgZone);
  private readonly destroyRef = inject(DestroyRef);
  private disposed = false;
  private loading = false;
  private controller?: StoryController;
  private observer?: IntersectionObserver;
  private heightObserver?: ResizeObserver;

  constructor() {
    this.destroyRef.onDestroy(() => {
      this.disposed = true;
      this.observer?.disconnect();
      this.heightObserver?.disconnect();
      this.controller?.dispose();
    });
  }

  ngAfterViewInit(): void {
    this.zone.runOutsideAngular(() => {
      const host = this.host.nativeElement;
      const reference = host.ownerDocument.querySelector<HTMLElement>('#vision .vision');
      if (!reference) return;
      const syncHeight = () => host.style.setProperty('--af-section-height', `${reference.getBoundingClientRect().height}px`);
      this.heightObserver = new ResizeObserver(syncHeight);
      this.heightObserver.observe(reference);
      syncHeight();
      this.observer = new IntersectionObserver(entries => {
        if (entries.some(entry => entry.isIntersecting)) void this.mount(reference);
      }, { rootMargin: '250px 0px' });
      this.observer.observe(host);
    });
  }

  private async mount(reference: HTMLElement): Promise<void> {
    if (this.loading || this.disposed) return;
    this.loading = true;
    this.observer?.disconnect();
    const fallback = this.stage.nativeElement.innerHTML;
    try {
      const url = new URL(FALAH_STORY_MODULE, this.host.nativeElement.ownerDocument.baseURI).href;
      const module: StoryModule = await import(/* @vite-ignore */ url);
      if (this.disposed) return;
      const controller = await module.mountSection(this.stage.nativeElement, {
        heightReference: reference,
        wrapper: this.stage.nativeElement,
        isCancelled: () => this.disposed
      });
      if (this.disposed) controller.dispose();
      else this.controller = controller;
    } catch {
      // The readable benefit summary stays available when motion cannot load.
      if (!this.disposed) this.stage.nativeElement.innerHTML = fallback;
    }
  }
}
