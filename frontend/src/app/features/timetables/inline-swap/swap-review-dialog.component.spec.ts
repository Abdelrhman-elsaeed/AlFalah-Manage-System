import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { SwapReviewDialogComponent } from './swap-review-dialog.component';

describe('SwapReviewDialogComponent', () => {
  let fixture: ComponentFixture<SwapReviewDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [SwapReviewDialogComponent, NoopAnimationsModule] }).compileComponents();
    fixture = TestBed.createComponent(SwapReviewDialogComponent);
    fixture.componentInstance.visible = true;
    fixture.componentInstance.proposal = {
      id: 'direct',
      kind: 'DirectSwap',
      color: 'Red',
      label: 'تبديل مباشر',
      errors: ['تعارض'],
      warnings: [],
      preview: []
    };
    fixture.componentInstance.alternatives = [{
      id: 'safe-three-way',
      kind: 'ThreeWaySwap',
      color: 'Green',
      label: 'تبديل ثلاثي آمن',
      errors: [],
      warnings: [],
      preview: []
    }];
    fixture.detectChanges();
  });

  it('offers explicit approval and rejection for an executable suggestion', () => {
    const accepted: string[] = [];
    let rejected = false;
    fixture.componentInstance.acceptSuggestion.subscribe(id => accepted.push(id));
    fixture.componentInstance.rejectSuggestion.subscribe(() => rejected = true);

    const buttons = fixture.debugElement.queryAll(By.css('.alternatives button'));
    expect(buttons.map(button => button.nativeElement.textContent.trim())).toEqual([
      'موافقة وتنفيذ الاقتراح',
      'لا، اختيار حصة أخرى'
    ]);

    buttons[0].triggerEventHandler('click');
    buttons[1].triggerEventHandler('click');

    expect(accepted).toEqual(['safe-three-way']);
    expect(rejected).toBeTrue();
  });
});
