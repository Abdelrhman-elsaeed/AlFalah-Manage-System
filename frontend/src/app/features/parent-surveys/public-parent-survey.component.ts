import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import {
  ParentSurveyRating,
  PublicParentSurvey,
  SubmitParentSurveyRequest
} from '../../core/models/parent-survey.models';
import { ParentSurveysService } from '../../core/services/parent-surveys.service';

interface AnswerState {
  rating: FormControl<ParentSurveyRating | null>;
  reason: FormControl<string>;
}

@Component({
  selector: 'app-public-parent-survey',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './public-parent-survey.component.html',
  styleUrls: ['./public-parent-survey.component.css']
})
export class PublicParentSurveyComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(ParentSurveysService);
  private token = '';

  readonly ParentSurveyRating = ParentSurveyRating;
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly successMessage = signal('تم استلام تقييمك بنجاح.');
  readonly loadError = signal('');
  readonly submitError = signal('');
  readonly survey = signal<PublicParentSurvey | null>(null);
  readonly answers = new Map<number, AnswerState>();

  readonly parentName = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.minLength(2), Validators.maxLength(150)]
  });
  readonly mobileNumber = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/^\+?[0-9\s()-]{8,20}$/)]
  });

  readonly ratingOptions = [
    { value: ParentSurveyRating.Weak, label: 'ضعيف', icon: 'pi pi-times-circle', tone: 'weak' },
    { value: ParentSurveyRating.Acceptable, label: 'مقبول', icon: 'pi pi-minus-circle', tone: 'acceptable' },
    { value: ParentSurveyRating.Good, label: 'جيد', icon: 'pi pi-check-circle', tone: 'good' },
    { value: ParentSurveyRating.VeryGood, label: 'جيد جدًا', icon: 'pi pi-star-fill', tone: 'very-good' }
  ] as const;

  ngOnInit(): void {
    this.token = this.route.snapshot.paramMap.get('token') ?? '';
    this.service.getPublic(this.token).subscribe({
      next: response => {
        if (response.isSuccess && response.data) {
          this.survey.set(response.data);
          response.data.items.forEach(item => this.answers.set(item.id, {
            rating: new FormControl<ParentSurveyRating | null>(null, Validators.required),
            reason: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(1000)] })
          }));
        } else {
          this.loadError.set(response.message || response.errors?.[0] || 'تعذر فتح النموذج.');
        }
        this.loading.set(false);
      },
      error: error => {
        this.loadError.set(error?.error?.message || 'الرابط غير صالح أو لم يعد متاحًا.');
        this.loading.set(false);
      }
    });
  }

  answerFor(itemId: number): AnswerState {
    return this.answers.get(itemId)!;
  }

  selectRating(itemId: number, rating: ParentSurveyRating): void {
    const answer = this.answerFor(itemId);
    answer.rating.setValue(rating);
    answer.rating.markAsTouched();
    if (rating !== ParentSurveyRating.Weak) answer.reason.setValue('');
  }

  submit(event: Event): void {
    // This form uses standalone FormControls rather than an Angular FormGroup.
    // Always intercept the native submit so the browser cannot navigate/reload.
    event.preventDefault();
    this.submitError.set('');
    this.parentName.markAsTouched();
    this.mobileNumber.markAsTouched();
    this.answers.forEach(answer => {
      answer.rating.markAsTouched();
      answer.reason.markAsTouched();
    });

    const hasInvalidAnswer = [...this.answers.values()].some(x => x.rating.invalid || x.reason.invalid);
    if (this.parentName.invalid || this.mobileNumber.invalid || hasInvalidAnswer) {
      this.submitError.set('أكمل الاسم ورقم الجوال واختر تقييمًا لكل بند.');
      this.scrollToError();
      return;
    }

    const currentSurvey = this.survey();
    if (!currentSurvey) return;
    const request: SubmitParentSurveyRequest = {
      parentName: this.parentName.value.trim(),
      mobileNumber: this.mobileNumber.value.trim(),
      answers: currentSurvey.items.map(item => {
        const answer = this.answerFor(item.id);
        return {
          itemId: item.id,
          rating: answer.rating.value!,
          weakReason: answer.rating.value === ParentSurveyRating.Weak
            ? answer.reason.value.trim() || undefined
            : undefined
        };
      })
    };

    this.submitting.set(true);
    this.service.submitPublic(this.token, request).subscribe({
      next: response => {
        this.submitting.set(false);
        if (response.isSuccess) {
          this.successMessage.set(response.message || 'تم استلام تقييمك بنجاح.');
          this.submitted.set(true);
          window.scrollTo({ top: 0, behavior: 'smooth' });
        } else {
          this.submitError.set(response.message || response.errors?.[0] || 'تعذر إرسال الرد.');
        }
      },
      error: error => {
        this.submitting.set(false);
        this.submitError.set(error?.error?.message || error?.error?.errors?.[0] || 'تعذر إرسال الرد. حاول مرة أخرى.');
      }
    });
  }

  useFallbackLogo(event: Event): void {
    (event.target as HTMLImageElement).src = 'assets/Logo.png';
  }

  private scrollToError(): void {
    setTimeout(() => document.querySelector('.field-error, .question.invalid')?.scrollIntoView({
      behavior: 'smooth',
      block: 'center'
    }));
  }
}
