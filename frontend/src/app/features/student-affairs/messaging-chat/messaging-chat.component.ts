import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';
import { EMPTY, filter, finalize, forkJoin, fromEvent, switchMap, timer } from 'rxjs';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import { ConversationDto, ConversationMessageDto, SendMessageResultDto } from '../../../core/models/phase5.models';
import { AuthService } from '../../../core/services/auth.service';
import { Phase5Service } from '../../../core/services/phase5.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-messaging-chat',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ButtonModule, DialogModule, InputTextModule, InputTextareaModule, ProgressSpinnerModule, TagModule],
  templateUrl: './messaging-chat.component.html',
  styleUrl: './messaging-chat.component.css'
})
export class MessagingChatComponent implements OnInit {
  private readonly api = inject(Phase5Service);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly conversations = signal<readonly ConversationDto[]>([]);
  readonly totalRecords = signal(0);
  readonly loadingInbox = signal(true);
  readonly inboxError = signal('');
  readonly unreadOnly = signal(false);
  readonly selected = signal<ConversationDto | null>(null);
  readonly messages = signal<readonly ConversationMessageDto[]>([]);
  readonly loadingThread = signal(false);
  readonly loadingOlder = signal(false);
  readonly hasOlder = signal(false);
  readonly sending = signal(false);
  readonly sendError = signal('');
  readonly pendingIdempotencyKey = signal<string | null>(null);
  readonly queuedResults = signal<ReadonlyMap<number, SendMessageResultDto>>(new Map<number, SendMessageResultDto>());
  readonly draft = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(4000)] });
  readonly closeDialogVisible = signal(false);
  readonly closeReason = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(2000)] });
  readonly closing = signal(false);

  get currentUserId(): string { return this.auth.currentUser()?.userId ?? ''; }
  get canClose(): boolean { return this.auth.hasPermission('Messaging.CloseThread'); }
  get isGuardian(): boolean { return this.auth.hasRole('Guardian'); }

  ngOnInit(): void {
    this.loadInbox();
    timer(25_000, 25_000).pipe(
      filter(() => typeof document === 'undefined' || document.visibilityState === 'visible'),
      switchMap(() => {
        const thread = this.selected();
        return thread ? this.api.getMessages(thread.id, 50) : EMPTY;
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(response => {
      if (response.isSuccess && response.data) this.mergeMessages(response.data.items);
    });
    if (typeof window !== 'undefined') {
      fromEvent(window, 'focus').pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.revalidateOpenThread());
    }
  }

  loadInbox(): void {
    this.loadingInbox.set(true);
    this.inboxError.set('');
    this.api.listConversations({ pageNumber: 1, pageSize: 100, isUnread: this.unreadOnly() || undefined }).pipe(finalize(() => this.loadingInbox.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) { this.inboxError.set(response.errors[0] ?? response.message ?? 'تعذر تحميل المحادثات.'); return; }
        this.conversations.set(response.data.items);
        this.totalRecords.set(response.data.totalCount);
        const selectedId = this.selected()?.id;
        if (selectedId) {
          const latest = response.data.items.find(item => item.id === selectedId);
          if (latest) this.selected.set(latest);
        }
      },
      error: error => this.inboxError.set(this.httpMessage(error, 'تعذر تحميل المحادثات.'))
    });
  }

  selectConversation(item: ConversationDto): void {
    this.selected.set(item);
    this.messages.set([]);
    this.hasOlder.set(false);
    this.sendError.set('');
    this.loadingThread.set(true);
    forkJoin({ header: this.api.getConversation(item.id), messages: this.api.getMessages(item.id, 50) }).pipe(finalize(() => this.loadingThread.set(false))).subscribe({
      next: ({ header, messages }) => {
        if (!header.isSuccess || !header.data || !messages.isSuccess || !messages.data) {
          this.toast.error('تعذر فتح المحادثة', header.errors[0] ?? messages.errors[0] ?? header.message ?? messages.message);
          return;
        }
        this.selected.set(header.data);
        this.messages.set(this.sortedUnique(messages.data.items));
        this.hasOlder.set(messages.data.hasNext || messages.data.hasPrevious || messages.data.totalCount > messages.data.items.length);
        this.markRenderedRead();
      },
      error: error => this.toast.error('تعذر فتح المحادثة', this.httpMessage(error, 'تحقق من صلاحية الوصول ثم حاول مجددًا.'))
    });
  }

  loadOlder(): void {
    const thread = this.selected();
    const oldest = this.messages()[0];
    if (!thread || !oldest || this.loadingOlder()) return;
    this.loadingOlder.set(true);
    this.api.getMessages(thread.id, 50, oldest.id).pipe(finalize(() => this.loadingOlder.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) return;
        this.messages.set(this.sortedUnique([...response.data.items, ...this.messages()]));
        this.hasOlder.set(response.data.items.length > 0 && response.data.totalCount > response.data.items.length);
        this.markRenderedRead();
      },
      error: error => this.toast.error('تعذر تحميل الرسائل الأقدم', this.httpMessage(error, 'حاول مرة أخرى.'))
    });
  }

  send(): void {
    const thread = this.selected();
    const body = this.draft.value.trim();
    if (!thread || thread.status !== 'Open' || !body || this.sending()) return;
    const key = this.pendingIdempotencyKey() ?? this.api.createIdempotencyKey();
    this.pendingIdempotencyKey.set(key);
    this.sending.set(true);
    this.sendError.set('');
    this.api.sendMessage(thread.id, { body, replyToMessageId: null, idempotencyKey: key }).pipe(finalize(() => this.sending.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) { this.sendError.set(response.errors[0] ?? response.message ?? 'لم تُرسل الرسالة.'); return; }
        this.appendSendResult(response.data);
        this.pendingIdempotencyKey.set(null);
        this.draft.reset('');
      },
      error: error => this.sendError.set(this.httpMessage(error, 'تعذر تأكيد حفظ الرسالة. ستستخدم إعادة المحاولة نفس مفتاح الإرسال لمنع التكرار.'))
    });
  }

  openCloseDialog(): void { this.closeReason.reset(''); this.closeDialogVisible.set(true); }
  closeThread(): void {
    const thread = this.selected();
    const reason = this.closeReason.value.trim();
    if (!thread || !reason || this.closing()) return;
    this.closing.set(true);
    this.api.closeConversation(thread.id, { reason, rowVersion: thread.rowVersion }).pipe(finalize(() => this.closing.set(false))).subscribe({
      next: response => {
        if (!response.isSuccess || !response.data) { this.toast.warn('لم تُغلق المحادثة', response.errors[0] ?? response.message); return; }
        this.acceptConversationUpdate(response.data);
        this.closeDialogVisible.set(false);
        this.toast.success('تم إغلاق المحادثة', 'أصبح صندوق الكتابة للقراءة فقط.');
      },
      error: (error: HttpErrorResponse) => {
        if (error.status === 409) {
          this.toast.warn('تغيرت المحادثة بواسطة مستخدم آخر', 'احتفظنا بسبب الإغلاق وجلبنا أحدث حالة قبل السماح بأي إجراء آخر.');
          this.revalidateHeader();
        } else this.toast.error('تعذر إغلاق المحادثة', this.httpMessage(error, 'حاول مرة أخرى.'));
      }
    });
  }

  isMine(message: ConversationMessageDto): boolean { return message.sender.userId === this.currentUserId; }
  queuedResult(messageId: number): SendMessageResultDto | null { return this.queuedResults().get(messageId) ?? null; }
  threadTypeLabel(type: ConversationDto['threadType']): string { return ({ GuardianTeacher: 'ولي الأمر والمعلم', GuardianStudentAffairs: 'ولي الأمر وشؤون الطلاب', GuardianSocialWorker: 'ولي الأمر والموجه الطلابي' })[type]; }
  deliveryLabel(state: ConversationMessageDto['deliveryState']): string { return ({ Pending: 'قيد الانتظار', Delivered: 'تم التسليم', Failed: 'تعذر التسليم' })[state]; }
  formatDateTime(value: string | null): string {
    if (!value) return '—';
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('ar-SA', { dateStyle: 'short', timeStyle: 'short' }).format(date);
  }

  private appendSendResult(result: SendMessageResultDto): void {
    this.mergeMessages([result.message]);
    this.queuedResults.update(current => {
      const next = new Map(current);
      next.set(result.message.id, result);
      return next;
    });
    if (result.disposition === 'QueuedUntilOfficeHours') {
      this.toast.info('الرسالة مجدولة للساعات المكتبية', result.nextEligibleSendAt ? `سيتم التنبيه في أقرب ساعة مكتبية: ${this.formatDateTime(result.nextEligibleSendAt)}` : 'سيتم تنبيه المعلم خلال فترة مكتبية قادمة.');
    } else if (result.disposition === 'BypassedForUrgency') {
      this.toast.warn('أُرسلت كحالة عاجلة', 'تم تسجيل التجاوز للتدقيق.');
    }
  }
  private revalidateOpenThread(): void {
    const thread = this.selected();
    if (!thread) { this.loadInbox(); return; }
    forkJoin({ header: this.api.getConversation(thread.id), messages: this.api.getMessages(thread.id, 50) }).subscribe({
      next: ({ header, messages }) => {
        if (header.isSuccess && header.data) this.acceptConversationUpdate(header.data);
        if (messages.isSuccess && messages.data) { this.mergeMessages(messages.data.items); this.markRenderedRead(); }
      }
    });
  }
  private revalidateHeader(): void {
    const thread = this.selected();
    if (!thread) return;
    this.api.getConversation(thread.id).subscribe({ next: response => { if (response.isSuccess && response.data) this.acceptConversationUpdate(response.data); } });
  }
  private acceptConversationUpdate(updated: ConversationDto): void {
    this.selected.set(updated);
    this.conversations.update(items => items.map(item => item.id === updated.id ? updated : item));
  }
  private mergeMessages(incoming: readonly ConversationMessageDto[]): void { this.messages.set(this.sortedUnique([...this.messages(), ...incoming])); }
  private sortedUnique(messages: readonly ConversationMessageDto[]): readonly ConversationMessageDto[] {
    return [...new Map(messages.map(message => [message.id, message])).values()].sort((a, b) => a.id - b.id);
  }
  private markRenderedRead(): void {
    const thread = this.selected();
    const highest = this.messages().at(-1)?.id;
    if (!thread || highest === undefined) return;
    this.api.markConversationRead(thread.id, { throughMessageId: highest }).subscribe({ next: response => { if (response.isSuccess) this.acceptConversationUpdate({ ...thread, unreadCount: 0 }); } });
  }
  private httpMessage(error: unknown, fallback: string): string { return extractHttpErrorMessage(error) ?? fallback; }
}
