import { CommonModule } from '@angular/common';
import { Component, DestroyRef, HostListener, OnInit, TemplateRef, ViewChild, computed, forwardRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { CalendarModule } from 'primeng/calendar';
import { InputTextModule } from 'primeng/inputtext';
import { filter, timer } from 'rxjs';
import { ClearableSelectComponent } from '../../../shared/components/clearable-select/clearable-select.component';
import { SchoolDutyService } from '../../../core/services/school-duty.service';
import { DutyChange, DutyDay, DutyHistory, DutyPreview, DutySettings, DutyWorkspace } from '../../../core/models/school-duty.models';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import { DutyExecutionComponent } from './duty-execution.component';
import { DutyRosterExportComponent } from './duty-roster-export.component';
import { DutyDocumentsComponent } from './duty-documents.component';
import { DutyAdminArchiveComponent } from './duty-admin-archive.component';
import { DutyPermissionsComponent } from './duty-permissions.component';
import { DutyErrorDirective } from './duty-error.directive';
import { DutyErrorDialogComponent } from './duty-error-dialog.component';
import { DutyFeedbackService } from './duty-feedback.service';
import { AuthService } from '../../../core/services/auth.service';
import { DUTY_SECTIONS, DUTY_WORKSPACE_NAVIGATION, DutySection, dutySectionRoute } from './duty-workspace-navigation';

@Component({
  selector: 'app-school-duty', standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, RouterLink, RouterOutlet, ButtonModule, DialogModule, CalendarModule, InputTextModule, ClearableSelectComponent, DutyExecutionComponent, DutyRosterExportComponent, DutyDocumentsComponent, DutyAdminArchiveComponent, DutyPermissionsComponent, DutyErrorDirective, DutyErrorDialogComponent],
  providers: [DutyFeedbackService, { provide: DUTY_WORKSPACE_NAVIGATION, useExisting: forwardRef(() => SchoolDutyComponent) }],
  templateUrl: './school-duty.component.html', styleUrls: ['./duty-dialog.css', './school-duty.component.css', './duty-pages.css', './duty-admin.css']
})
export class SchoolDutyComponent implements OnInit {
  private readonly api = inject(SchoolDutyService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy = inject(DestroyRef);
  private readonly auth = inject(AuthService);
  readonly feedback = inject(DutyFeedbackService);
  readonly data = signal<DutyWorkspace | null>(null);
  readonly section = signal<DutySection>('schedule');
  setupStage: 'teachers' | 'calendar' | 'review' = 'teachers';
  @ViewChild('schedulePage', { static: true }) schedulePage!: TemplateRef<unknown>;
  @ViewChild('settingsPage', { static: true }) settingsPage!: TemplateRef<unknown>;
  @ViewChild('executionPage', { static: true }) executionPage!: TemplateRef<unknown>;
  @ViewChild('documentsPage', { static: true }) documentsPage!: TemplateRef<unknown>;
  @ViewChild('fairnessPage', { static: true }) fairnessPage!: TemplateRef<unknown>;
  @ViewChild('permissionsPage', { static:true }) permissionsPage!:TemplateRef<unknown>;
  @ViewChild('historyPage', { static: true }) historyPage!: TemplateRef<unknown>;
  executionId: number | null = null;
  readonly executionContext = signal<DutyWorkspace | null>(null);
  readonly executionLoading = signal(false);
  inspection = false;
  private executionContextKey = '';
  private executionRequestSequence = 0;
  @ViewChild(DutyExecutionComponent) execution?: DutyExecutionComponent;
  readonly language = signal(this.translate.currentLang || 'ar');
  readonly loading = signal(true); readonly busy = signal(false); readonly error = signal(''); readonly success = signal('');
  readonly view = signal<'week' | 'term'>('week'); readonly week = signal(''); readonly filter = signal('all'); readonly teacher = signal<string | null>(null);
  readonly page = signal(0);
  readonly pageSize = 10;
  readonly ownToday = computed(() => this.data()?.days.find(x => x.date === this.data()?.today && x.isMine));
  readonly today = computed(() => this.data()?.days.find(x => x.date === this.data()?.today));
  readonly next = computed(() => this.data()?.days.filter(x => x.date >= (this.data()?.today ?? '') && x.isMine).sort((a,b) => a.date.localeCompare(b.date))[0]);
  readonly exceptions = computed(() => this.data()?.days.filter(x => x.exceptionCode).length ?? 0);
  readonly debts = computed(() => this.data()?.history.filter(x => x.unresolved) ?? []);
  readonly ownCount = computed(() => this.data()?.days.filter(x => x.isMine).length ?? 0);
  readonly weeks = computed(() => [...new Set(this.data()?.days.map(x => this.weekOf(x.date)) ?? [])]);
  readonly visibleDays = computed(() => (this.data()?.days ?? []).filter(x =>
    (!this.teacher() || x.teacherId === this.teacher()) &&
    (this.filter() === 'all' || this.filter() === 'exceptions' && !!x.exceptionCode || this.filter() === 'mine' && x.isMine || this.filter() === 'exchanged' && x.teacherId !== x.originalTeacherId)));
  readonly groups = computed(() => [...new Set(this.visibleDays().map(x => this.weekOf(x.date)))].map(week => ({ week, days: this.visibleDays().filter(x => this.weekOf(x.date) === week) })));
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.visibleDays().length / this.pageSize)));
  readonly currentPage = computed(() => Math.min(this.page(), this.pageCount() - 1));
  readonly pageDays = computed(() => this.visibleDays().slice(this.currentPage() * this.pageSize, (this.currentPage() + 1) * this.pageSize));
  selectScheduleFilter(value: string) { this.filter.set(value); this.teacher.set(null); this.page.set(0); }
  selectScheduleTeacher(value: string | null) { this.teacher.set(value); this.filter.set('all'); this.page.set(0); }
  movePage(delta: number) { this.page.set(Math.max(0, Math.min(this.currentPage() + delta, this.pageCount() - 1))); }
  readonly termOptions = computed(() => (this.data()?.terms ?? []).map(x => ({ id: x.id, name: `${this.t('TERM')} ${x.semester} · ${x.year}` })));
  readonly teacherOptions = computed(() => (this.data()?.teachers ?? []).filter(x => x.isEligible !== false));
  readonly scheduleTeachers = computed(() => [...new Map((this.data()?.days ?? []).map(x => [x.teacherId, { userId: x.teacherId, name: x.teacher }])).values()]);
  readonly order = computed(() => this.draft()?.teacherIds.map(id => this.data()?.teachers.find(x => x.userId === id)).filter(Boolean) ?? []);
  readonly draft = signal<DutySettings | null>(null);
  setupOpen = false; previewOpen = false; manualOpen = false; detailOpen = false; historyOpen = false;
  preview: DutyPreview | null = null; manualPreview: DutyPreview | null = null; selected: DutyDay | null = null;
  search = ''; termId: number | null = null; reason = ''; substitute: string | null = null; otherId: number | null = null;
  teacherPage=0;rotationPage=0;customDismissal=false;historyCursor=0;fairnessCursor=0;previewPage=0;
  get historyPages(){return Math.max(1,Math.ceil((this.data()?.history.length??0)/10));}
  get displayedHistory(){return (this.data()?.history??[]).slice(Math.min(this.historyCursor,this.historyPages-1)*10,(Math.min(this.historyCursor,this.historyPages-1)+1)*10);}
  moveHistoryPage(delta:number){this.historyCursor=Math.max(0,Math.min(this.historyPages-1,this.historyCursor+delta));}
  get fairnessPages(){return Math.max(1,Math.ceil((this.data()?.fairness.length??0)/10));}
  get displayedFairness(){return (this.data()?.fairness??[]).slice(Math.min(this.fairnessCursor,this.fairnessPages-1)*10,(Math.min(this.fairnessCursor,this.fairnessPages-1)+1)*10);}
  moveFairnessPage(delta:number){this.fairnessCursor=Math.max(0,Math.min(this.fairnessPages-1,this.fairnessCursor+delta));}
  get teacherPages(){return Math.max(1,Math.ceil(this.roster.length/10));}
  get displayedTeachers(){return this.roster.slice(Math.min(this.teacherPage,this.teacherPages-1)*10,(Math.min(this.teacherPage,this.teacherPages-1)+1)*10);}
  moveTeacherPage(delta:number){this.teacherPage=Math.max(0,Math.min(this.teacherPages-1,this.teacherPage+delta));}
  get rotationPages(){return Math.max(1,Math.ceil(this.order().length/10));}
  get rotationOffset(){return Math.min(this.rotationPage,this.rotationPages-1)*10;}
  get displayedOrder(){return this.order().slice(this.rotationOffset,this.rotationOffset+10);}
  moveRotationPage(delta:number){this.rotationPage=Math.max(0,Math.min(this.rotationPages-1,this.rotationPage+delta));}
  setDismissalMode(custom:boolean){this.customDismissal=custom;if(!custom)this.draft.update(x=>({...x!,dismissalAt:null}));this.invalidatePublication();}
  setDismissal(value:string){this.draft.update(x=>({...x!,dismissalAt:value?value+':00':null}));this.invalidatePublication();}
  mode = 'Exchange'; acceptException = false; resolveId: number | null = null; manualRequest: DutyChange | null = null;
  exclusionTeacher: string | null = null; exclusionFrom: Date | null = null; exclusionTo: Date | null = null; exclusionReason = '';
  exclusionMode: 'permanent' | 'dated' = 'permanent';
  closureFrom: Date | null = null; closureTo: Date | null = null; closureReason = '';
  private publishRequest: Parameters<SchoolDutyService['publish']>[0] | null = null;
  private settingsBaseline='';
  private initialDismissalMode=false;
  private settingsIdentity(){return JSON.stringify({...this.draft(),dismissalAt:this.draft()?.dismissalAt??null});}
  settingsChanged(){return !!this.draft()&&(this.settingsIdentity()!==this.settingsBaseline||this.customDismissal!==this.initialDismissalMode);}
  private loadSequence = 0;
  private refreshingReview = false;
  private readonly dateCache = new Map<string, Date>();
  readonly weekdays = [0,1,2,3,4,5,6];
  constructor() {
    // PrimeNG 17 binds Escape when a dialog opens and does not re-check a later
    // closeOnEscape change. Own the top modal's Escape without losing settings.
    const escape = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      if (this.feedback.current()) { event.preventDefault(); event.stopImmediatePropagation(); return; }
      const target = event.target instanceof Element ? event.target : null;
      const control = target?.closest('.p-calendar,.p-datepicker,.p-dropdown,.p-dropdown-panel');
      const popupOpen = Array.from(document.querySelectorAll<HTMLElement>('.p-datepicker,.p-dropdown-panel')).some(x => x.getBoundingClientRect().width > 0 && x.getBoundingClientRect().height > 0);
      if (control && popupOpen) return;
      this.closeTopDialog(event);
    };
    document.addEventListener('keydown', escape, true);
    this.destroy.onDestroy(() => document.removeEventListener('keydown', escape, true));
  }
  readonly filteredTeachers = computed(() => this.data()?.teachers ?? []);
  readonly swapDates = computed(() => (this.data()?.days ?? []).filter(x => !x.locked && x.date >= (this.data()?.today ?? '') && x.id !== this.selected?.id && x.teacherId === this.substitute).map(x => ({ id: x.id, name: `${this.date(x.date)} · ${x.teacher}` })));
  // Plain editable dialog fields are intentionally evaluated on change detection.
  get availableSwapDates() { return (this.data()?.days ?? []).filter(x => !x.locked && x.date >= (this.data()?.today ?? '') && x.id !== this.selected?.id && x.teacherId === this.substitute).map(x => ({ id: x.id, name: this.date(x.date) })); }
  get availableCompensationDates() { return (this.data()?.days ?? []).filter(x => !x.locked && x.date >= (this.data()?.today ?? '')).map(x => ({ id: x.id, name: `${this.date(x.date, true)} · ${x.teacher}` })); }
  get roster() { return (this.data()?.teachers ?? []).filter(x => !this.search || x.name.includes(this.search)); }
  ngOnInit() {
    const term = Number(this.route.snapshot.queryParamMap.get('termId')); if (term > 0) this.termId = term;
    this.translate.onLangChange.pipe(takeUntilDestroyed(this.destroy)).subscribe(event => this.language.set(event.lang));
    this.router.events.pipe(filter(event => event instanceof NavigationEnd), takeUntilDestroyed(this.destroy)).subscribe(() => this.syncNavigation(true));
    this.load(); timer(10000, 10000).pipe(takeUntilDestroyed(this.destroy)).subscribe(() => {
      if (!this.executionId && !this.hasDraft() && !this.detailOpen && !this.historyOpen && !this.busy() && document.visibilityState === 'visible') this.load(true);
    });
  }
  t(key: string) { this.language(); return this.translate.instant(`DUTY.${key}`); }
  get sectionOptions() { return DUTY_SECTIONS.filter(x => (!x.managerOnly || this.data()?.canManage)&&(!x.ownerOnly||this.data()?.canDelegate)).map(x => ({ ...x, key: !this.data()?.canManage && x.id === 'execution' ? 'DUTY_PAGES.MY_EXECUTION' : !this.data()?.canManage && x.id === 'documents' ? 'DUTY_PAGES.MY_DOCUMENTS' : x.key })); }
  trackSection(_: number, item: { id: DutySection }) { return item.id; }
  sectionRoute = dutySectionRoute;
  sectionKey() { return this.sectionOptions.find(x => x.id === this.section())?.key ?? 'DUTY_PAGES.SCHEDULE'; }
  pageTemplate(section: DutySection) { return this.data() || section === 'schedule' ? this[`${section}Page`] ?? null : null; }
  activateSection(section: DutySection) {
    const changed = this.section() !== section;
    this.section.set(section); this.syncNavigation();
    if (section !== 'execution') { this.executionRequestSequence++; this.executionContextKey=''; this.executionLoading.set(false); }
    if (changed) setTimeout(() => document.querySelector<HTMLElement>('.shell-content')?.scrollTo({ top: 0, behavior: 'instant' as ScrollBehavior }));
  }
  canLeaveSection() { return !this.busy() && !this.execution?.busy(); }
  get executionOptions() {
    const d = this.data(); if (!d) return [];
    const days = (d.days ?? []).filter(x => d.canManage || x.isMine).map(x => ({ id: x.id, date: x.date, name: `${this.date(x.date, true)} · ${x.teacher}` }));
    for (const old of d.ownHistory ?? []) if (!days.some(x => x.id === old.assignmentId)) days.push({ id: old.assignmentId, date: old.date, name: `${this.date(old.date, true)} · ${this.t('YOU')}` });
    return days.sort((a,b) => b.date.localeCompare(a.date));
  }
  private syncNavigation(settled=false) {
    // Child activation can precede the parent query snapshot update. Read historical
    // selection only after NavigationEnd, so an early today request cannot replace it.
    if (!settled && this.router.getCurrentNavigation()) return;
    const linked = Number(this.route.snapshot.queryParamMap.get('execution'));
    if (this.section() === 'execution') {
      this.inspection = this.route.snapshot.queryParamMap.get('review') === 'true' && this.route.snapshot.queryParamMap.get('resume') !== 'true';
      if (this.route.snapshot.queryParamMap.get('review') === 'true' && linked > 0) {
        this.executionRequestSequence++;this.executionContextKey='';
        this.executionId = linked; this.executionLoading.set(false); return;
      }
      this.loadExecutionDay(); return;
    }
    if (linked > 0) this.executionId = linked;
    if (this.section() === 'settings' && this.data() && !this.draft()) this.openSetup();
    if (['execution','documents'].includes(this.section()) && !this.executionId && this.data()) {
      const preferred = this.data()!.days.find(x => x.date >= this.data()!.today && (this.data()!.canManage || x.isMine));
      this.executionId = preferred?.id ?? this.executionOptions[0]?.id ?? null;
    }
  }
  private loadExecutionDay(force=false) {
    if (!this.data()) return;
    const key=`${this.auth.activeSchoolId()}:${this.auth.currentUser()?.userId}:${this.data()!.today}`;
    if (!force && this.executionContextKey===key) return;
    this.executionContextKey=key;const sequence=++this.executionRequestSequence;
    this.executionLoading.set(true);this.executionContext.set(null);this.executionId=null;this.inspection=false;
    this.api.context(null).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r=>{
      if(sequence!==this.executionRequestSequence || this.section()!=='execution')return;
      this.executionLoading.set(false);
      if(!r.isSuccess || !r.data){this.error.set(r.message||this.t('ERROR'));this.executionContextKey='';return;}
      this.executionContext.set(r.data);
      const linked=Number(this.route.snapshot.queryParamMap.get('execution'));
      const linkedDate=this.route.snapshot.queryParamMap.get('date');
      if(linked>0 && linkedDate && linkedDate!==r.data.today){void this.router.navigate(['/school-duty/documents'],{queryParamsHandling:'preserve',replaceUrl:true});return;}
      const day=r.data.days.find(x=>x.date===r.data!.today && (r.data!.canManage || x.isMine));
      this.executionId=day?.id??null;
      if(this.route.snapshot.queryParamMap.get('execution')!==String(this.executionId??''))void this.router.navigate([],{relativeTo:this.route,queryParams:{execution:this.executionId,date:null,review:null,resume:null},queryParamsHandling:'merge',replaceUrl:true});
    },error:e=>{if(sequence!==this.executionRequestSequence || this.section()!=='execution')return;this.executionLoading.set(false);this.executionContextKey='';this.error.set(extractHttpErrorMessage(e)||this.t('ERROR'));}});
  }
  get canResumeArchive() {
    const context=this.data();const day=context?.days.find(x=>x.id===this.executionId);
    return !!context && !context.canManage && !!day?.isMine && day.date<=context.today
      && !(context.ownHistory??[]).some(x=>x.assignmentId===day.id && x.assignmentRevision===day.revision && x.authorUserId===context.userId && x.submitted);
  }
  openArchiveDuty(resume=false) {
    if(!this.executionId || resume && !this.canResumeArchive)return;
    void this.router.navigate(['/school-duty/execution'],{queryParams:{execution:this.executionId,termId:this.termId,review:true,resume:resume||null},replaceUrl:true});
  }
  selectExecution(id: number) {
    if (!id || id===this.executionId || !this.canLeaveSection()) return;
    const option = this.executionOptions.find(x => x.id === id); this.executionId = id;
    void this.router.navigate([dutySectionRoute(this.section())], { queryParams: { execution: id, date: option?.date }, queryParamsHandling: 'merge', replaceUrl: true });
  }
  trackExecution(_: number, id: number | null) { return id; }
  goToSettings() { void this.router.navigate(['/school-duty/settings'], { queryParamsHandling: 'preserve' }); }
  cancelSettings() { this.setupOpen = false; this.previewOpen = false; this.preview = null; this.draft.set(null); this.publishRequest = null; this.setupStage = 'teachers'; void this.router.navigate(['/school-duty']); }
  invalidatePublication() { this.previewOpen = false; this.preview = null; this.publishRequest = null; }
  selectSetupStage(stage: 'teachers' | 'calendar' | 'review') {
    if (stage === 'review') { this.previewPlan(); return; }
    this.setupStage = stage;
    setTimeout(() => document.getElementById('duty-stage-' + stage)?.focus());
  }
  setupKey(event: KeyboardEvent) {
    const tabs = Array.from(document.querySelectorAll<HTMLButtonElement>('.settings-steps [role=tab]:not(:disabled)'));
    const index = tabs.indexOf(event.target as HTMLButtonElement); const forward = this.dir === 'rtl' ? 'ArrowLeft' : 'ArrowRight';
    const backward = this.dir === 'rtl' ? 'ArrowRight' : 'ArrowLeft'; let next = index;
    if (event.key === forward) next = (index + 1) % tabs.length;
    else if (event.key === backward) next = (index - 1 + tabs.length) % tabs.length;
    else if (event.key === 'Home') next = 0; else if (event.key === 'End') next = tabs.length - 1; else return;
    event.preventDefault(); tabs[next]?.focus();
  }
  get dir() { return this.language() === 'en' ? 'ltr' : 'rtl'; }
  date(value: string, weekday = false) { return new Intl.DateTimeFormat(this.dir === 'rtl' ? 'ar-SA-u-ca-gregory' : 'en-GB', { day: 'numeric', month: 'short', ...(weekday ? { weekday: 'long' as const } : {}) }).format(this.asDate(value)); }
  weekday(value: string) { return new Intl.DateTimeFormat(this.dir === 'rtl' ? 'ar-SA' : 'en-GB', { weekday: 'long' }).format(this.asDate(value)); }
  dayNumber(value: string) { return this.asDate(value).getDate(); }
  asDate(value: string) { let date = this.dateCache.get(value); if (!date) { date = new Date(`${value}T12:00:00`); this.dateCache.set(value, date); } return date; }
  iso(date: Date) { return `${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,'0')}-${String(date.getDate()).padStart(2,'0')}`; }
  weekOf(date: string) { const d = new Date(this.asDate(date)); d.setDate(d.getDate()-d.getDay()); return this.iso(d); }
  hasDraft() {
    const d = this.data(); const cached = d ? this.executionOptions.some(x => sessionStorage.getItem(`duty-draft:${d.userId}:${x.id}`)) : false;
    return this.settingsChanged() || (this.manualOpen && (!!this.reason.trim() || !!this.otherId || !!this.substitute)) || this.busy() || !!this.execution?.dirty() || !!this.execution?.busy() || cached;
  }
  @HostListener('window:beforeunload', ['$event']) beforeUnload(event: BeforeUnloadEvent) { if (this.hasDraft()) event.preventDefault(); }
  closeTopDialog(event: KeyboardEvent) {
    if (!(this.manualOpen || this.detailOpen || this.historyOpen)) return;
    event.preventDefault(); event.stopImmediatePropagation();
    if (this.busy()) return;
    if (this.manualOpen) this.manualOpen = false;
    else if (this.detailOpen) this.closeDetails();
    else if (this.historyOpen) this.historyOpen = false;
  }
  load(background = false) {
    const sequence = ++this.loadSequence;
    if (!background) { this.loading.set(true); this.error.set(''); }
    this.api.context(this.termId).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: r => {
      if (sequence !== this.loadSequence) return;
      this.loading.set(false);
      if (!r.isSuccess || !r.data) { this.error.set(r.message || this.t('ERROR')); return; }
      this.data.set(r.data); this.termId = r.data.termId;
      if (this.refreshingReview) {
        this.refreshingReview = false;
        if (this.selected) this.selected = r.data.days.find(x => x.id === this.selected?.id) ?? this.selected;
        this.invalidateManual(); this.previewOpen = false; this.preview = null; this.publishRequest = null;
        this.success.set(this.t('REVIEW_REFRESHED'));
      }
      if (!this.week() || !this.weeks().includes(this.week())) this.week.set(this.weeks().find(x => x >= this.weekOf(r.data!.today)) ?? this.weeks()[0] ?? '');
      this.syncNavigation();
      const linked = this.route.snapshot.queryParamMap.get('date');
      if (linked && !background) { const linkedDay = r.data.days.find(x => x.date === linked); if (linkedDay) { this.week.set(this.weekOf(linked)); this.selected = linkedDay;
        if (this.section() === 'schedule' && !this.route.snapshot.queryParamMap.has('execution')) { if (linkedDay.isMine && !r.data.canManage) this.openExecution(linkedDay); else this.detailOpen = true; } } }
    }, error: e => { if (sequence !== this.loadSequence) return; this.loading.set(false); if (e.status === 403) this.data.set(null); if (!background || e.status === 403) this.error.set(extractHttpErrorMessage(e) || this.t('ERROR')); } });
  }
  switchTerm(id: number) {
    if (!id || id===this.termId) return;
    if (this.settingsChanged() && !window.confirm(this.t('LEAVE_DRAFT'))) return;
    this.draft.set(null); this.setupOpen = false; this.invalidatePublication(); this.executionId = null;
    this.termId = id; this.week.set(''); this.load(); void this.router.navigate([], { relativeTo: this.route, queryParams: { termId: id, execution: null, date: null }, queryParamsHandling: 'merge', replaceUrl: true });
  }
  moveWeek(delta: number) { const index = this.weeks().indexOf(this.week()); this.week.set(this.weeks()[Math.max(0, Math.min(this.weeks().length-1,index+delta))] ?? ''); }
  applyFilter(value: string) { this.filter.set(value); this.view.set('term'); }
  status(day: DutyDay) {
    if (day.exceptionCode) return this.t(`CODE_${day.exceptionCode}`);
    if (day.protected || ['Replace','Exchange','ResolveCompensation'].includes(day.changeKind || '')) return this.t('MANUAL');
    if (day.changeKind === 'Regenerate') return this.t('REGENERATED');
    if (day.changeKind === 'Undo') return this.t('CORRECTED');
    if (['AutoExchange','UnresolvedCompensation'].includes(day.changeKind || '')) return this.t('EXCHANGED');
    return this.t(day.teacherId !== day.originalTeacherId ? 'ADJUSTED' : 'SCHEDULED');
  }
  attendance(day: DutyDay) { return this.t(`ATTENDANCE_${day.attendance || 'Unknown'}`); }
  initials(name: string) { return name.split(' ').filter(Boolean).slice(0,2).map(x => x[0]).join(' '); }
  openDetails(day: DutyDay) { if(day.isMine && !this.data()?.canManage && day.date===this.data()?.today){this.openExecution(day);return;} this.selected = day; this.detailOpen = true; void this.router.navigate([], { relativeTo: this.route, queryParams: { date: day.date }, queryParamsHandling: 'merge', replaceUrl: true }); }
  openExecution(day: DutyDay) { this.detailOpen=false;this.executionId=day.id;void this.router.navigate(['/school-duty/execution'], {queryParams:{date:day.date,execution:day.id,review:day.date!==this.data()?.today?true:null},queryParamsHandling:'merge',replaceUrl:true}); }
  openHistorical(id:number,date:string){this.executionId=id;void this.router.navigate(['/school-duty/execution'], {queryParams:{date,execution:id},queryParamsHandling:'merge',replaceUrl:true});}
  closeExecution() { this.executionId=null;void this.router.navigate(['/school-duty'], {queryParams:{date:null,execution:null},queryParamsHandling:'merge',replaceUrl:true});this.load(); }
  closeDetails() { this.detailOpen = false; void this.router.navigate([], { relativeTo: this.route, queryParams: { date: null }, queryParamsHandling: 'merge', replaceUrl: true }); }
  dayHistory() { return this.data()?.history.filter(x => x.date === this.selected?.date || x.otherDate === this.selected?.date) ?? []; }
  historyReason(history: DutyHistory) {
    if(history.kind==='SettingsTimingPreserved')return this.t('TIMING_RETAINED_REASON');
    if (history.kind === 'Publish' || history.kind === 'Regenerate') return this.t(`KIND_${history.kind}`);
    if (history.actor !== 'system') return history.reason;
    if (history.kind === 'AutoExchange' || history.kind === 'UnresolvedCompensation') return this.t(history.reason.includes('Excused') ? 'AUTOMATIC_EXCUSED' : 'AUTOMATIC_ABSENT');
    return this.t(`KIND_${history.kind}`);
  }
  compensation(day: DutyDay) { const history = this.data()?.history ?? []; return history.find(x => x.date === day.date && x.kind === 'AutoExchange' && !history.some(u => u.kind === 'Undo' && u.relatedChangeId === x.id))?.otherDate; }
  openSetup() {
    const d = this.data(); const term = d?.terms.find(x => x.id === d.termId); if (!d || !term) return;
    this.error.set(''); this.search = ''; this.preview = null; this.setupStage = 'teachers';
    const eligible = d.teachers.filter(t => t.isEligible !== false).map(t => t.userId);
    const excluded = d.settings?.permanentlyExcludedTeacherIds ?? (d.settings ? eligible.filter(id => !d.settings!.teacherIds.includes(id)) : []);
    const order = [...new Set([...(d.settings?.teacherIds ?? []), ...eligible])].filter(id => eligible.includes(id) && !excluded.includes(id));
    this.draft.set(d.settings ? { ...structuredClone(d.settings), teacherIds: order, permanentlyExcludedTeacherIds: [...excluded], weekdays: [...d.suggestedWeekdays] } : { termId: term.id, from: term.from, to: term.to, weekdays: [...d.suggestedWeekdays], teacherIds: order, permanentlyExcludedTeacherIds: [], exclusions: [], closures: [], regenerateFrom: d.today > term.from && d.today <= term.to ? d.today : term.from, regenerateTo: term.to, acceptAttendanceCalendarMismatch: false });
    this.exclusionTeacher = null; this.exclusionMode = 'permanent'; this.exclusionReason = ''; this.exclusionFrom = null; this.exclusionTo = null;
    if (d.settings) { this.draft.update(x => ({ ...x!, regenerateFrom: d.today > x!.from && d.today <= x!.to ? d.today : x!.from, regenerateTo: x!.to })); }
    this.setupOpen = true;
    this.teacherPage=0;this.rotationPage=0;this.customDismissal=!!this.draft()?.dismissalAt;
    this.settingsBaseline=this.settingsIdentity();this.initialDismissalMode=this.customDismissal;
  }
  setDate(field: 'from'|'to'|'regenerateFrom'|'regenerateTo', value: Date) { if (value) this.draft.update(x => ({ ...x!, [field]: this.iso(value) })); }
  excludePermanently(id: string, returnFocus = false) {
    this.draft.update(x => ({ ...x!, teacherIds: x!.teacherIds.filter(t => t !== id), permanentlyExcludedTeacherIds: [...new Set([...(x!.permanentlyExcludedTeacherIds ?? []), id])] }));
    this.rotationPage=Math.min(this.rotationPage,this.rotationPages-1);
    if (returnFocus) setTimeout(() => document.getElementById('duty-restore-' + id)?.focus());
  }
  restorePermanent(id: string, returnFocus = false) {
    this.draft.update(x => ({ ...x!, teacherIds: this.teacherOptions().some(t => t.userId === id) ? [...new Set([...x!.teacherIds, id])] : x!.teacherIds, permanentlyExcludedTeacherIds: x!.permanentlyExcludedTeacherIds?.filter(t => t !== id) ?? [] }));
    if (returnFocus) setTimeout(() => document.getElementById('duty-exclude-' + id)?.focus());
  }
  chooseDatedExclusion(id: string) {
    this.exclusionTeacher = id; this.exclusionMode = 'dated';
    const s = this.draft()!; const today = this.data()!.today;
    this.exclusionFrom ??= this.asDate(today >= s.from && today <= s.to ? today : s.from);
    this.exclusionTo ??= this.exclusionFrom;
    setTimeout(() => { document.getElementById('duty-exclusions')?.scrollIntoView({ block: 'start' }); document.getElementById('duty-exclude-from')?.focus(); });
  }
  reorder(index: number, delta: number) { const ids = [...this.draft()!.teacherIds]; if (index+delta < 0 || index+delta >= ids.length) return; [ids[index],ids[index+delta]] = [ids[index+delta],ids[index]]; this.draft.update(x => ({ ...x!, teacherIds: ids })); }
  addExclusion() {
    if (!this.exclusionTeacher || this.exclusionMode === 'dated' && (!this.exclusionFrom || !this.exclusionTo || !this.exclusionReason.trim())) { this.error.set(this.t('FIELDS_REQUIRED')); return; }
    if (this.exclusionMode === 'permanent') this.excludePermanently(this.exclusionTeacher);
    else {
      if (this.exclusionFrom! > this.exclusionTo!) { this.error.set(this.t('EXCLUSION_DATE_ERROR')); return; }
      const exclusion = { userId: this.exclusionTeacher, from: this.iso(this.exclusionFrom!), to: this.iso(this.exclusionTo!), reason: this.exclusionReason.trim() };
      this.draft.update(x => ({ ...x!, exclusions: x!.exclusions.some(e => JSON.stringify(e) === JSON.stringify(exclusion)) ? x!.exclusions : [...x!.exclusions, exclusion] }));
    }
    this.exclusionReason = ''; this.error.set('');
  }
  addClosure() { if (!this.closureFrom || !this.closureTo || !this.closureReason.trim()) { this.error.set(this.t('FIELDS_REQUIRED')); return; } this.draft.update(x => ({ ...x!, closures: [...x!.closures, { from: this.iso(this.closureFrom!), to: this.iso(this.closureTo!), reason: this.closureReason.trim() }] })); this.closureReason = ''; this.error.set(''); }
  restore(index: number) { this.draft.update(x => ({ ...x!, exclusions: x!.exclusions.filter((_,i) => i !== index) })); }
  removeClosure(index: number) { this.draft.update(x => ({ ...x!, closures: x!.closures.filter((_,i) => i !== index) })); }
  teacherName(id: string) { return this.data()?.teachers.find(x => x.userId === id)?.name ?? id; }
  previewPlan() {
    if(this.customDismissal&&!this.draft()?.dismissalAt){this.error.set(this.translate.instant('DUTY_ADMIN.TIME_REQUIRED'));return;}
    this.previewPage=0;
    this.busy.set(true); this.error.set(''); this.api.preview(this.draft()!, this.data()!.planRevision).subscribe({ next: r => {
      this.busy.set(false); if (!r.data || !r.isSuccess) { this.error.set(r.message || this.t('ERROR')); return; }
      this.preview = r.data; this.publishRequest = { settings: structuredClone(this.draft()!), expectedRevision: this.data()!.planRevision, previewDigest: r.data.digest, requestId: crypto.randomUUID() }; this.previewOpen = true; this.setupStage = 'review'; setTimeout(() => document.getElementById('duty-stage-review')?.focus());
    }, error: e => this.fail(e) });
  }
  publish() {
    if (!this.publishRequest) return; this.busy.set(true); this.error.set('');
    this.api.publish(this.publishRequest).subscribe({ next: r => { this.busy.set(false); if (!r.isSuccess) { this.error.set(r.message || this.t('ERROR')); return; } this.previewOpen = false; this.setupOpen = false; this.draft.set(null); this.publishRequest = null; this.success.set(this.t('PUBLISHED')); void this.router.navigate(['/school-duty']).then(navigated => { if (navigated) this.load(); }); }, error: e => this.fail(e) });
  }
  openManual(day: DutyDay, mode = 'Exchange', debt?: DutyHistory) {
    this.closeDetails(); this.selected = day; this.manualOpen = true; this.mode = mode; this.reason = ''; this.substitute = mode === 'Unprotect' ? day.teacherId : null;
    if (debt) { this.resolveId = debt.id; this.substitute = debt.beforeTeacherId; }
    else this.resolveId = null;
    this.otherId = null; this.acceptException = false; this.manualPreview = null; this.manualRequest = null; this.error.set('');
  }
  resolve(debt: DutyHistory) {
    if (!this.data()?.teachers.some(x => x.userId === debt.beforeTeacherId && x.isEligible !== false)) { this.error.set(this.t('COMPENSATION_INELIGIBLE')); return; }
    const day = this.data()?.days.find(x => x.date >= this.data()!.today && !x.locked); if (day) this.openManual(day, 'ResolveCompensation', debt); else this.error.set(this.t('NO_FUTURE_DATE'));
  }
  selectCompensationDate(id: number) { this.selected = this.data()?.days.find(x => x.id === id) ?? this.selected; this.invalidateManual(); }
  invalidateManual() { this.manualPreview = null; this.manualRequest = null; }
  reviewManual() {
    if (!this.selected || !this.substitute || !this.reason.trim()) { this.error.set(this.t('FIELDS_REQUIRED')); return; }
    this.manualRequest = { assignmentId: this.selected.id, expectedRevision: this.data()!.planRevision, teacherId: this.substitute, otherAssignmentId: this.mode === 'Exchange' ? this.otherId : null,
      reason: this.reason.trim(), mode: this.mode, acceptException: true, resolveChangeId: this.resolveId, requestId: crypto.randomUUID() };
    this.busy.set(true); this.error.set(''); this.api.changePreview(this.manualRequest).subscribe({ next: r => { this.busy.set(false); if (r.isSuccess && r.data) { this.manualPreview = r.data; setTimeout(() => document.getElementById(r.data!.warnings.length ? 'duty-exception-accept' : 'duty-confirm-change')?.focus()); } else this.error.set(r.message || this.t('ERROR')); }, error: e => this.fail(e) });
  }
  confirmManual() {
    if (!this.manualRequest || !this.manualPreview || this.manualPreview.warnings.length && !this.acceptException) return;
    this.busy.set(true); this.api.change(this.manualRequest).subscribe({ next: r => { this.busy.set(false); if (!r.isSuccess) { this.error.set(r.message || this.t('ERROR')); return; } this.manualOpen = false; this.success.set(this.t('CHANGED')); this.load(); }, error: e => this.fail(e) });
  }
  retry() { this.busy.set(true); this.api.retry().subscribe({ next: () => { this.busy.set(false); this.success.set(this.t('RETRY_QUEUED')); this.load(); }, error: e => this.fail(e) }); }
  refreshForReview() { if(this.section()==='execution' && this.route.snapshot.queryParamMap.get('review')!=='true'){this.loadExecutionDay(true);return;}this.refreshingReview = true; this.load(true); }
  private fail(e: any) { this.busy.set(false); if (e?.status === 403) this.data.set(null); this.error.set(extractHttpErrorMessage(e) || this.t('ERROR')); }
}
