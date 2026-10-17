import { CommonModule } from '@angular/common';
import { Component, DestroyRef, HostListener, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { DialogModule } from 'primeng/dialog';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiResponse, PagedResult } from '../../../core/models/api-response.model';
import { CoverageAction, CoverageNeed, CoverageTask } from '../../../core/models/daily-coverage.models';
import { DailyCoverageService } from '../../../core/services/daily-coverage.service';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';

@Component({selector:'app-coverage-tasks',standalone:true,imports:[CommonModule,FormsModule,RouterLink,TranslateModule,DialogModule],templateUrl:'./coverage-tasks.component.html',styleUrls:['./coverage-tasks.component.css']})
export class CoverageTasksComponent {
  private readonly api=inject(DailyCoverageService); private readonly destroy=inject(DestroyRef);
  private readonly translate=inject(TranslateService); private readonly route=inject(ActivatedRoute);
  tasks:PagedResult<CoverageNeed>|null=null; detail:CoverageTask|null=null;
  busy=false; writing=false; error=''; notice=''; page=1; narrative=''; notes='';
  review:'acknowledge'|'submit'|'unable'|null=null; reason='';
  pending:{action:'acknowledge'|'submit'|'unable';body:CoverageAction}|null=null;
  get dirty(){return !!this.detail&&(this.narrative!==this.detail.need.narrative||this.notes!==this.detail.need.notes);}
  constructor(){void this.initialize();}
  async initialize(){await this.load();const id=Number(this.route.snapshot.queryParamMap.get('needId'));if(id)await this.open(id);}
  canLeave(){return !this.writing&&(!this.dirty||window.confirm(this.translate.instant('COVERAGE.LEAVE_DRAFT')));}
  @HostListener('window:beforeunload',['$event']) beforeUnload(e:BeforeUnloadEvent){if(this.dirty||this.writing){e.preventDefault();e.returnValue='';}}
  private async read<T>(o:Observable<ApiResponse<T>>){const r=await firstValueFrom(o.pipe(takeUntilDestroyed(this.destroy)));if(!r.isSuccess||r.data==null)throw Error(r.errors?.[0]||r.message);return r.data;}
  private async run(action:()=>Promise<void>,writing=false){if(this.busy)return;this.busy=true;this.writing=writing;this.error='';this.notice='';try{await action();}catch(e){this.error=extractHttpErrorMessage(e)||this.translate.instant('COVERAGE.REQUEST_FAILED');}finally{this.busy=false;this.writing=false;}}
  async load(){await this.run(async()=>{this.tasks=await this.read(this.api.tasks(this.page));});}
  async open(id:number){if(!this.canLeave())return;await this.run(async()=>{this.adopt(await this.read(this.api.task(id)));});}
  private adopt(d:CoverageTask){this.detail=d;this.narrative=d.need.narrative;this.notes=d.need.notes;this.pending=null;this.review=null;}
  async save(){if(!this.detail||!this.detail.canEditDraft)return;await this.run(async()=>{const n=this.detail!.need;this.adopt(await this.read(this.api.draft(n.id,{revision:n.revision,substitutionId:n.substitutionId!,narrative:this.narrative,notes:this.notes})));this.notice=this.translate.instant('COVERAGE.DRAFT_SAVED');},true);}
  prepare(action:'acknowledge'|'submit'|'unable'){
    if(this.busy||!this.detail)return;
    if(this.dirty){this.error=this.translate.instant('COVERAGE.SAVE_FIRST');return;}
    if(this.pending?.action!==action){this.pending=null;this.reason='';}this.review=action;
  }
  async confirm(){if(!this.detail||!this.review)return;const action=this.review,n=this.detail.need;
    if(action==='unable'&&!this.reason.trim())return;
    await this.run(async()=>{this.pending??={action,body:{requestId:crypto.randomUUID(),revision:n.revision,substitutionId:n.substitutionId!,reason:this.reason.trim()||undefined}};
      const result=await this.read(this.api.act(n.id,this.pending.action,this.pending.body));this.adopt(result);this.notice=this.translate.instant('COVERAGE.ACTION_SAVED');if(this.tasks)this.tasks={...this.tasks,items:this.tasks.items.map(item=>item.id===result.need.id?result.need:item)};},true);
  }
  label(state:string){return this.translate.instant('COVERAGE.STATE_'+state);}
  time(n:CoverageNeed){return new Intl.DateTimeFormat(this.translate.currentLang==='en'?'en-GB':'ar-EG',{timeZone:n.timeZoneId,hour:'2-digit',minute:'2-digit'}).format(new Date(n.startsAt))+' – '+new Intl.DateTimeFormat('en-GB',{timeZone:n.timeZoneId,hour:'2-digit',minute:'2-digit'}).format(new Date(n.endsAt));}
  async changePage(delta:number){if(!this.canLeave())return;this.page+=delta;await this.load();}
}
