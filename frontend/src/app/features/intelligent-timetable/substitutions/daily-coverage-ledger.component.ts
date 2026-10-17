import { CommonModule } from '@angular/common';
import { Component, DestroyRef, ViewChild, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { DialogModule } from 'primeng/dialog';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiResponse } from '../../../core/models/api-response.model';
import { CoverageAction, CoverageApproval, CoverageBoard, CoverageChoice, CoverageNeed, CoverageOption, CoveragePreview, CoveragePreviewRequest, CoverageTask } from '../../../core/models/daily-coverage.models';
import { DailyCoverageService } from '../../../core/services/daily-coverage.service';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import { CoverageRegisterComponent } from './coverage-register.component';

@Component({selector:'app-daily-coverage-ledger',standalone:true,imports:[CommonModule,FormsModule,RouterLink,TranslateModule,DialogModule,CoverageRegisterComponent],templateUrl:'./daily-coverage-ledger.component.html',styleUrls:['./daily-coverage-ledger.component.css']})
export class DailyCoverageLedgerComponent {
  private readonly api=inject(DailyCoverageService);
  private readonly destroy=inject(DestroyRef);
  private readonly translate=inject(TranslateService);
  board: CoverageBoard|null=null; busy=false; error=''; notice='';
  date=new Date().toLocaleDateString('en-CA'); search=''; state='all'; page=1;
  mode:'Manual'|'Fair'='Fair'; choices:CoverageChoice[]=[];
  selected:CoverageNeed|null=null; options:CoverageOption[]=[]; candidateSearch='';
  proposal:CoveragePreview|null=null; reviewedRequest:CoveragePreviewRequest|null=null; reviewOpen=false; reason='';
  private approval:CoverageApproval|null=null;
  detail:CoverageTask|null=null; adminAction:'reopen'|'keep'|'release'|null=null; adminReason='';
  registerOpen=false;@ViewChild(CoverageRegisterComponent) register?:CoverageRegisterComponent;
  constructor(){void this.load(false);}
  private async read<T>(request:Observable<ApiResponse<T>>):Promise<T>{const r=await firstValueFrom(request.pipe(takeUntilDestroyed(this.destroy)));if(!r.isSuccess||r.data==null)throw Error(r.errors?.[0]||r.message);return r.data;}
  private async run(action:()=>Promise<void>){if(this.busy)return;this.busy=true;this.error='';this.notice='';try{await action();}catch(e){this.error=extractHttpErrorMessage(e)||this.translate.instant('COVERAGE.REQUEST_FAILED');}finally{this.busy=false;}}
  async load(reconcile:boolean){await this.run(async()=>{this.board=await this.read(reconcile?this.api.reconcile(this.date):this.api.board(this.date));this.choices=[];this.invalidate();this.page=1;});}
  invalidate(){this.proposal=null;this.reviewedRequest=null;this.approval=null;}
  setMode(mode:'Manual'|'Fair'){if(this.busy)return;this.mode=mode;this.invalidate();}
  get filtered(){return this.board?.needs.filter(n=>(this.state==='all'||n.state===this.state)&&[n.originalTeacher,n.classroom,n.subject,n.assignedTeacher||''].join(' ').includes(this.search.trim()))??[];}
  get rows(){return this.filtered.slice((this.page-1)*10,this.page*10);}
  get pages(){return Math.max(1,Math.ceil(this.filtered.length/10));}
  count(state:string){return this.board?.needs.filter(n=>n.state===state).length??0;}
  get candidates(){return this.options.filter(o=>o.name.includes(this.candidateSearch.trim()));}
  chosen(n:CoverageNeed){return this.choices.find(c=>c.needId===n.id);}
  chosenName(n:CoverageNeed){const id=this.chosen(n)?.teacherId;return id?this.board?.teachers.find(t=>t.id===id)?.name:null;}
  label(state:string){return this.translate.instant('COVERAGE.STATE_'+state);}
  time(n:CoverageNeed){const f=(v:string)=>new Intl.DateTimeFormat(this.translate.currentLang==='en'?'en-GB':'ar-EG',{timeZone:n.timeZoneId,hour:'2-digit',minute:'2-digit'}).format(new Date(v));return f(n.startsAt)+' – '+f(n.endsAt);}
  async chooseTeacher(n:CoverageNeed){await this.run(async()=>{this.options=await this.read(this.api.options(n.id));this.selected=n;this.candidateSearch='';});}
  choose(o:CoverageOption){if(this.busy||!this.selected||o.color==='Red'||o.color==='Yellow'&&!this.board?.canOverride)return;this.choices=this.choices.filter(c=>c.needId!==this.selected!.id);this.choices.push({needId:this.selected.id,teacherId:o.teacherId});this.selected=null;this.invalidate();}
  remove(n:CoverageNeed){this.choices=this.choices.filter(c=>c.needId!==n.id);this.invalidate();}
  canLeave(){return !this.busy&&(this.register?.canLeave()??true);}
  async preview(){await this.run(async()=>{const current=await this.read(this.api.board(this.date));const request:CoveragePreviewRequest={date:this.date,mode:this.mode,choices:structuredClone(this.choices),expiresAt:new Date(Date.parse(current.now)+4.5*60000).toISOString()};this.proposal=await this.read(this.api.preview(request));this.reviewedRequest=request;this.reviewOpen=true;this.reason='';this.approval=null;});}
  get requiresReason(){return this.proposal?.assignments.some(a=>a.color==='Yellow'||this.board?.needs.find(n=>n.id===a.needId)?.substitutionId!=null)??false;}
  async approve(){if(!this.proposal||!this.reviewedRequest||this.requiresReason&&!this.reason.trim())return;await this.run(async()=>{
    this.approval??={requestId:crypto.randomUUID(),proposalId:this.proposal!.proposalId,preview:structuredClone(this.reviewedRequest!),reason:this.reason.trim()||null};
    this.board=await this.read(this.api.approve(this.approval));this.reviewOpen=false;this.choices=[];this.invalidate();this.notice=this.translate.instant('COVERAGE.APPROVED');
  });}
  needName(id:number){const n=this.board?.needs.find(n=>n.id===id);return n?`${n.originalTeacher} · ${n.classroom} · ${n.subject} · ${n.periods.join('، ')}`:'';}
  async show(n:CoverageNeed){await this.run(async()=>{this.detail=await this.read(this.api.task(n.id));this.adminAction=null;this.adminReason='';});}
  async decide(){if(!this.detail||!this.adminAction||!this.adminReason.trim())return;const n=this.detail.need;await this.run(async()=>{
    const body:CoverageAction={requestId:crypto.randomUUID(),revision:n.revision,substitutionId:n.substitutionId!,reason:this.adminReason.trim()};
    this.detail=await this.read(this.api.act(n.id,this.adminAction!,body));this.adminAction=null;if(this.board)this.board={...this.board,needs:this.board.needs.map(item=>item.id===this.detail!.need.id?this.detail!.need:item)};this.invalidate();this.notice=this.translate.instant('COVERAGE.DECISION_SAVED');
  });}
}
