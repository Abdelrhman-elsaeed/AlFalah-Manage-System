import { CommonModule } from '@angular/common';
import { Component, DestroyRef, EventEmitter, HostListener, Input, OnInit, Output, ViewChild, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { DialogModule } from 'primeng/dialog';
import { Subject, debounceTime, firstValueFrom, timer } from 'rxjs';
import { DutyExecutionService } from '../../../core/services/duty-execution.service';
import { DutyExecution, DutyLate, DutyReportContent, DutyStudent } from '../../../core/models/duty-execution.models';
import { DutyStudentPickerComponent } from './duty-student-picker.component';
import { AuthService } from '../../../core/services/auth.service';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import { DutyErrorDirective } from './duty-error.directive';

@Component({ selector: 'app-duty-execution', standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, DialogModule, DutyStudentPickerComponent, DutyErrorDirective],
  templateUrl: './duty-execution.component.html', styleUrls: ['./duty-execution.component.css', './duty-execution-dialog.css', './duty-execution-flow.css'] })
export class DutyExecutionComponent implements OnInit {
  @Input({ required: true }) assignmentId = 0; @Output() closed = new EventEmitter<void>();
  @Input() inspection = false;
  @ViewChild(DutyStudentPickerComponent) picker?: DutyStudentPickerComponent;
  private readonly api = inject(DutyExecutionService); readonly translate = inject(TranslateService);
  private readonly router = inject(Router); private readonly auth = inject(AuthService); private readonly destroy = inject(DestroyRef);
  readonly data = signal<DutyExecution | null>(null); readonly loading = signal(true); readonly busy = signal(false);
  readonly error = signal(''); readonly saved = signal(''); readonly dirty = signal(false); readonly conflict = signal<DutyExecution | null>(null);
  content: DutyReportContent = { noLateStudents: false, noViolations: false, violations: [], observations: '', lastDeparture: null };
  step: 'letter' | 'report' | 'review' = 'letter'; letterOpen = false; factOpen = false; historyOpen = false;
  category: 'late' | 'violation' = 'late'; student: DutyStudent | null = null; observedAt = ''; departureAt = ''; action = ''; notes = '';
  factError = ''; correctionReason = ''; timingAt = ''; managerReason = ''; managerAction: 'report' | 'timing' | null = null;
  correctingFact: DutyLate | null = null; retract = false;
  private draftVersion = 0; private editSequence = 0; private changes = new Subject<void>();
  private signAttempt: { action: 'acknowledge' | 'report/submit'; version: number; revision: number; key: string } | null = null;
  private factAttempt: { digest: string; key: string } | null = null;
  get dir() { return this.translate.currentLang === 'en' ? 'ltr' : 'rtl'; }
  get cacheKey() { return `duty-draft:${this.auth.currentUser()?.userId}:${this.assignmentId}`; }
  get activeFacts() { return this.data()?.lateStudents.filter(x => !x.isRetracted) ?? []; }
  get recordedIds() { return this.activeFacts.map(x => x.studentId); }
  get readonly() { return !this.data()?.canExecute || !!this.data()?.draft?.submittedAt || this.inspection && !this.data()?.draft?.correctionReason; }
  t(key: string) { return this.translate.instant(`DUTY_EXEC.${key}`); }
  ngOnInit() {
    this.load(); this.changes.pipe(debounceTime(850), takeUntilDestroyed(this.destroy)).subscribe(() => { if (!this.conflict() && !this.readonly) void this.save(); });
    timer(5000,5000).pipe(takeUntilDestroyed(this.destroy)).subscribe(() => {
      if (this.data() && !this.busy() && !this.factOpen && !this.dirty() && document.visibilityState === 'visible')
        this.api.get(this.assignmentId).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r => {
          if (!r.data || !r.isSuccess || this.busy() || this.dirty() || this.factOpen || r.data.assignmentRevision !== this.data()?.assignmentRevision) return;
          if (r.data.draft?.version !== this.data()?.draft?.version) {
            this.content = structuredClone(r.data.draft?.content ?? this.content);
            this.draftVersion = r.data.draft?.version ?? 0;
            this.saved.set(r.data.draft?.savedAt ?? '');
            this.signAttempt = null;
          }
          this.data.set(r.data);
        },error:e=>{if(e.status===403){this.data.set(null);this.error.set(this.t('LOAD_ERROR'));}}});
    });
  }
  load() {
    this.loading.set(true); this.api.get(this.assignmentId).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: r => {
      this.loading.set(false); if (!r.isSuccess || !r.data) { this.error.set(r.message || this.t('LOAD_ERROR')); return; }
      this.data.set(r.data); this.content = structuredClone(r.data.draft?.content ?? this.content); this.draftVersion = r.data.draft?.version ?? 0;
      this.saved.set(r.data.draft?.savedAt ?? ''); this.step = r.data.acknowledged ? 'report' : 'letter';
      const cached = sessionStorage.getItem(this.cacheKey);
      if (cached && !this.readonly) {
        try { const local = JSON.parse(cached); this.content = local.content; this.dirty.set(true);
          if (local.version !== this.draftVersion || local.revision !== r.data.assignmentRevision) this.conflict.set(r.data);
          else this.changes.next(); } catch { sessionStorage.removeItem(this.cacheKey); }
      }
    }, error: e => { this.loading.set(false); this.error.set(extractHttpErrorMessage(e) || this.t('LOAD_ERROR')); } });
  }
  changed() { this.editSequence++; this.dirty.set(true); this.signAttempt = null; this.cache(); this.changes.next(); }
  private cache() { sessionStorage.setItem(this.cacheKey, JSON.stringify({ content: this.content, version: this.draftVersion, revision: this.data()?.assignmentRevision })); }
  async save(): Promise<boolean> {
    const data = this.data(); if (!data || this.readonly || this.conflict()) return false;
    if (!this.dirty() && data.draft) return true;
    if (this.busy()) { this.changes.next(); return false; }
    if (this.content.violations.some(x => !x.action.trim() || !x.time)) return false;
    this.busy.set(true); this.error.set(''); const sequence = this.editSequence; const content = structuredClone(this.content);
    try {
      const r = await firstValueFrom(this.api.save(data.assignmentId, data.assignmentRevision, this.draftVersion, content));
      if (!r.isSuccess || !r.data) throw new Error(r.message || this.t('SAVE_ERROR'));
      this.data.set(r.data); this.draftVersion = r.data.draft!.version; this.saved.set(r.data.draft!.savedAt);
      if (sequence === this.editSequence) { this.dirty.set(false); sessionStorage.removeItem(this.cacheKey); }
      else { this.cache(); this.changes.next(); }
      return true;
    } catch (e: any) {
      this.error.set(extractHttpErrorMessage(e) || e.message || this.t('SAVE_ERROR'));
      this.cache();
      if (e.status === 409) {
        try { const r = await firstValueFrom(this.api.get(this.assignmentId)); if (r.data) this.conflict.set(r.data); }
        catch { /* Keep the original save error and cached input when the conflict refresh also fails. */ }
      }
      return false;
    } finally { this.busy.set(false); }
  }
  useServer() { const server = this.conflict(); if (!server) return; this.data.set(server); this.content = structuredClone(server.draft?.content ?? { noLateStudents:false,noViolations:false,violations:[],observations:'',lastDeparture:null }); this.draftVersion = server.draft?.version ?? 0; this.conflict.set(null); this.dirty.set(false); this.error.set(''); sessionStorage.removeItem(this.cacheKey); }
  keepLocal() { const server = this.conflict(); if (!server || !server.canExecute || server.draft?.submittedAt || server.assignmentRevision !== this.data()?.assignmentRevision) return;
    this.draftVersion = server.draft?.version ?? 0; this.data.set(server); this.conflict.set(null); this.changed(); }
  reviewConflict(){document.querySelector('.conflict-panel')?.scrollIntoView({behavior:'auto',block:'center'});}
  async review() {
    if ((!this.activeFacts.length && !this.content.noLateStudents) || (!this.content.violations.length && !this.content.noViolations) || !this.content.lastDeparture) {
      this.error.set(this.t('REVIEW_REQUIRED')); return;
    }
    if (await this.save()) { this.step = 'review'; document.querySelector('.execution-workspace')?.scrollIntoView({ block: 'start' }); }
  }
  async selectStep(step: 'letter' | 'report' | 'review') {
    if (step === 'review') { await this.review(); return; }
    if (step === 'report' && !this.data()?.acknowledged && this.data()?.canExecute) return;
    this.step = step;
  }
  openDocuments() { void this.router.navigate(['/school-duty/documents'], { queryParams: { execution: this.assignmentId, date: this.data()?.date }, queryParamsHandling: 'merge' }); }
  async sign(action: 'acknowledge' | 'report/submit') {
    const data = this.data(); if (!data || this.busy() || !data.canExecute) return;
    if (!data.signatureData) { this.error.set(this.t('SIGNATURE_MISSING')); return; }
    if (action === 'report/submit' && this.dirty() && !await this.save()) return;
    const revision = this.data()!.assignmentRevision;
    if (!this.signAttempt || this.signAttempt.action !== action || this.signAttempt.version !== this.draftVersion || this.signAttempt.revision !== revision)
      this.signAttempt = { action,version:this.draftVersion,revision,key:crypto.randomUUID() };
    this.busy.set(true); this.error.set('');
    try { const r = await firstValueFrom(this.api.sign(data.assignmentId, action, revision, this.signAttempt.key, action==='report/submit'?this.draftVersion:undefined));
      if (!r.isSuccess || !r.data) throw new Error(r.message || this.t('SAVE_ERROR'));
      this.data.set(r.data); this.draftVersion=r.data.draft?.version??0; this.signAttempt=null; this.step='report'; this.dirty.set(false); sessionStorage.removeItem(this.cacheKey);
    } catch(e:any) { this.error.set(extractHttpErrorMessage(e)||e.message||this.t('SAVE_ERROR')); if(e.error?.errorCode==='DutySignatureRequired')this.data.update(d=>d?{...d,signatureData:null}:d); }
    finally {this.busy.set(false);}
  }
  async signatureSettings() { if (this.dirty()) await this.save(); if (this.dirty()) this.cache();
    await this.router.navigate(['/account/signature'], {queryParams:{returnUrl:`/timetable?tab=duty&date=${this.data()?.date}&execution=${this.assignmentId}`}}); }
  openPicker(category:'late'|'violation') { this.category=category; this.picker?.open(); }
  picked(student:DutyStudent) {
    const existing=this.data()?.lateStudents.find(x=>x.studentId===student.studentId);
    this.student=student; this.correctingFact=null; this.factError=''; this.observedAt=''; this.departureAt=''; this.action=''; this.notes=''; this.factAttempt=null;
    if(this.category==='late'&&existing){this.factError=this.t('ALREADY_RECORDED');this.observedAt=existing.observedAt.slice(0,5);this.departureAt=existing.departureAt?.slice(0,5)??'';this.action=existing.action;this.notes=existing.notes;}
    this.factOpen=true;
  }
  async confirmFact() {
    const d=this.data(); if(!d||!this.student||this.busy()||!this.observedAt||!this.action.trim())return;
    if(this.category==='violation'){
      if(this.content.violations.some(x=>x.studentId===this.student!.studentId)){this.factError=this.t('ALREADY_RECORDED');return;}
      this.content.violations.push({studentId:this.student.studentId,enrollmentId:this.student.enrollmentId,studentName:this.student.name,classroom:this.student.classroom,time:this.observedAt+':00',action:this.action,notes:this.notes});
      this.content.noViolations=false;this.factOpen=false;this.changed();return;
    }
    const request={assignmentRevision:d.assignmentRevision,studentId:this.student.studentId,enrollmentId:this.student.enrollmentId,observedAt:this.observedAt.slice(0,5)+':00',departureAt:this.departureAt?this.departureAt.slice(0,5)+':00':null,action:this.action,notes:this.notes};
    const digest=JSON.stringify(request); if(this.factAttempt?.digest!==digest)this.factAttempt={digest,key:crypto.randomUUID()};
    this.busy.set(true);this.factError='';
    try{const r=await firstValueFrom(this.api.late(d.assignmentId,{...request,requestId:this.factAttempt!.key}));if(!r.data||!r.isSuccess)throw new Error(r.message||this.t('SAVE_ERROR'));
      const fresh=await firstValueFrom(this.api.get(d.assignmentId));if(fresh.data)this.data.set(fresh.data);this.content.noLateStudents=false;this.factOpen=false;this.factAttempt=null;this.changed();
    }catch(e:any){this.factError=extractHttpErrorMessage(e)||e.message||this.t('SAVE_ERROR');}finally{this.busy.set(false);}
  }
  closeFact(){this.factOpen=false;setTimeout(()=>document.getElementById(this.category==='late'?'add-late':'add-violation')?.focus(),30);}
  editFact(f:DutyLate){this.category='late';this.correctingFact=f;this.student={studentId:f.studentId,enrollmentId:f.enrollmentId,classroomId:f.classroomId,name:f.studentName,classroom:f.classroom,grade:0,studentNumber:''};this.observedAt=f.observedAt.slice(0,5);this.departureAt=f.departureAt?.slice(0,5)??'';this.action=f.action;this.notes=f.notes;this.correctionReason='';this.retract=f.isRetracted;this.factError='';this.factOpen=true;}
  async correctFact(){const f=this.correctingFact;if(!f||!this.correctionReason.trim())return;this.busy.set(true);this.factError='';try{const r=await firstValueFrom(this.api.correctLate(f.id,{expectedRevision:f.revision,observedAt:this.observedAt.slice(0,5)+':00',departureAt:this.departureAt?this.departureAt.slice(0,5)+':00':null,action:this.action,notes:this.notes,reason:this.correctionReason,retract:this.retract,requestId:crypto.randomUUID()}));if(!r.isSuccess)throw new Error(r.message);this.factOpen=false;this.load();}catch(e:any){this.factError=extractHttpErrorMessage(e)||e.message;}finally{this.busy.set(false);}}
  async managerConfirm(){const d=this.data();if(!d||!this.managerReason.trim())return;this.busy.set(true);this.error.set('');try{const r=await firstValueFrom(this.managerAction==='report'?this.api.correct(d.assignmentId,d.assignmentRevision,d.draft!.version,this.managerReason,crypto.randomUUID()):this.api.timing(d.assignmentId,d.assignmentRevision,this.timingAt?this.timingAt+':00':null,this.managerReason));if(!r.isSuccess||!r.data)throw new Error(r.message);this.data.set(r.data);this.managerAction=null;}catch(e:any){this.error.set(extractHttpErrorMessage(e)||e.message);}finally{this.busy.set(false);}}
  async retry(f:DutyLate){this.busy.set(true);try{await firstValueFrom(this.api.retry(f.id));const r=await firstValueFrom(this.api.get(this.assignmentId));if(r.data)this.data.set(r.data);}catch(e){this.error.set(extractHttpErrorMessage(e)||this.t('SAVE_ERROR'));}finally{this.busy.set(false);}}
  async close(){if(this.busy())return;if(this.dirty()){if(!await this.save())return;}this.closed.emit();}
  @HostListener('window:beforeunload',['$event']) unload(event:BeforeUnloadEvent){if(this.dirty()){this.cache();event.preventDefault();event.returnValue='';}}
  notice(f:DutyLate){return f.notices.at(-1);}
  async openSnapshot(id:number){try{const r=await firstValueFrom(this.api.snapshot(id));if(r.data){this.signedSnapshot=r.data.Snapshot;this.snapshotSignature=r.data.SignatureData;this.snapshotOpen=true;}}catch(e){this.error.set(extractHttpErrorMessage(e)||this.t('LOAD_ERROR'));}}
  snapshotOpen=false;signedSnapshot:any=null;snapshotSignature='';
}
