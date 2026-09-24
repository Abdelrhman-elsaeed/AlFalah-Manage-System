import {
  AfterViewInit,
  Component,
  DestroyRef,
  ElementRef,
  EventEmitter,
  HostListener,
  Output,
  ViewChild,
  inject,
  signal
} from '@angular/core';
import { NavigationStart, Router } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-landing-header',
  standalone: true,
  imports: [TranslateModule],
  templateUrl: './landing-header.component.html',
  styleUrls: ['./landing-header.component.css']
})
export class LandingHeaderComponent implements AfterViewInit {
  @Output() readonly loginRequested = new EventEmitter<void>();
  readonly menuOpen = signal(false);
  readonly isScrolled = signal(false);
  readonly activeSection = signal('home');
  readonly links = ['home', 'vision', 'organization', 'achievements', 'news', 'contact'];
  @ViewChild('menuToggle') menuToggle?: ElementRef<HTMLButtonElement>;
  private readonly destroyRef = inject(DestroyRef);
  private readonly desktop = window.matchMedia('(min-width: 769px)');
  private readonly resizeMenu = () => {
    if (this.desktop.matches) this.closeMenu();
  };

  constructor() {
    inject(Router)
      .events.pipe(takeUntilDestroyed())
      .subscribe((event) => {
        if (event instanceof NavigationStart) this.closeMenu();
      });
    this.desktop.addEventListener('change', this.resizeMenu);
    this.destroyRef.onDestroy(() => this.desktop.removeEventListener('change', this.resizeMenu));
  }
  ngAfterViewInit(): void {
    this.onScroll();
    const observer = new IntersectionObserver(
      (entries) => {
        entries
          .filter((entry) => entry.isIntersecting)
          .forEach((entry) => this.activeSection.set(entry.target.id));
      },
      { rootMargin: '-15% 0px -35% 0px', threshold: 0 }
    );
    this.links.forEach((id) => {
      const element = document.getElementById(id);
      if (element) observer.observe(element);
    });
    this.destroyRef.onDestroy(() => observer.disconnect());
  }
  @HostListener('window:scroll') onScroll(): void {
    this.isScrolled.set(window.scrollY > 24);
  }
  @HostListener('document:keydown.escape') onEscape(): void {
    if (this.menuOpen()) {
      this.closeMenu();
      this.menuToggle?.nativeElement.focus();
    }
  }
  closeMenu(): void {
    this.menuOpen.set(false);
  }
  requestLogin(event: MouseEvent): void {
    event.preventDefault();
    this.closeMenu();
    this.loginRequested.emit();
  }
  navigate(event: MouseEvent, id: string): void {
    event.preventDefault();
    this.closeMenu();
    this.activeSection.set(id);
    const target = document.getElementById(id);
    target?.setAttribute('tabindex', '-1');
    target?.focus({ preventScroll: true });
    target?.scrollIntoView({
      behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth'
    });
    history.replaceState(history.state, '', `#${id}`);
  }
}
