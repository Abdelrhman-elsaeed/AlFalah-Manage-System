import { CommonModule } from '@angular/common';
import { Component,DestroyRef,Input,OnChanges,SimpleChanges,ViewChild,inject,signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { TranslateModule,TranslateService } from '@ngx-translate/core';
import { DutyArchivePage,DutyArchiveRow,DutyDocumentState,DutyDocumentsService } from '../../../core/services/duty-documents.service';
import { DutyTeacher } from '../../../core/models/school-duty.models';
import { ClearableSelectComponent } from '../../../shared/components/clearable-select/clearable-select.component';
import { DutyDocumentsComponent } from './duty-documents.component';
import { DutyErrorDirective } from './duty-error.directive';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';

@Component({selector:'app-duty-admin-archive',standalone:true,imports:[CommonModule,FormsModule,TranslateModule,ClearableSelectComponent,DutyDocumentsComponent,DutyErrorDirective],
 templateUrl:'./duty-admin-archive.component.html',styleUrls:['./duty-admin-archive.component.css']})
export class DutyAdminArchiveComponent implements OnChanges {
 @Input() termId:number|null=null;@Input() teachers:DutyTeacher[]=[];
 @ViewChild(DutyDocumentsComponent) actions?:DutyDocumentsComponent;
 private readonly api=inject(DutyDocumentsService);private readonly translate=inject(TranslateService);private readonly destroy=inject(DestroyRef);
 readonly data=signal<DutyArchivePage|null>(null);readonly loading=signal(false);readonly error=signal('');
 teacher:string|null=null;from='';to='';page=1;private sequence=0;
 ngOnChanges(changes:SimpleChanges){if(changes['termId']){this.page=1;this.data.set(null);this.load();}}
 t(key:string){return this.translate.instant('DUTY_ADMIN.'+key);}
 get pages(){return Math.max(1,Math.ceil((this.data()?.total??0)/10));}
 get busy(){return this.loading()||!!this.actions?.busy();}
 filter(){this.page=1;this.load();}
 move(delta:number){if(this.busy)return;this.page=Math.max(1,Math.min(this.pages,this.page+delta));this.load();}
 load(){
  const sequence=++this.sequence;this.loading.set(true);this.error.set('');
  const query:Record<string,string|number>={page:this.page};if(this.termId)query['termId']=this.termId;if(this.teacher)query['teacherId']=this.teacher;if(this.from)query['from']=this.from;if(this.to)query['to']=this.to;
  this.api.archive(query).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r=>{if(sequence!==this.sequence)return;this.loading.set(false);if(!r.isSuccess||!r.data){this.error.set(r.message||this.t('ERROR'));return;}this.data.set(r.data);if(this.page>this.pages){this.page=this.pages;this.load();}},error:e=>{if(sequence!==this.sequence)return;this.loading.set(false);if(e.status===403||e.status===404){this.data.set(null);if(this.actions){this.actions.previewOpen=false;this.actions.closePreview();this.actions.historyOpen=false;}}this.error.set(extractHttpErrorMessage(e)||this.t('ERROR'));}});
 }
 open(row:DutyArchiveRow,doc:DutyDocumentState,download=false){this.actions?.openForAssignment(row.assignmentId,doc,download);}
 history(row:DutyArchiveRow){void this.actions?.historyForAssignment(row.assignmentId);}
 retry(doc:DutyDocumentState){void this.actions?.retry(doc);}
 enable(){if(this.busy)return;this.loading.set(true);this.api.enable(true).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:()=>this.load(),error:e=>{this.loading.set(false);this.error.set(extractHttpErrorMessage(e)||this.t('ERROR'));}});}
 track(_:number,row:DutyArchiveRow){return `${row.assignmentId}:${row.teacherId}:${row.revision}`;}
 status(doc:DutyDocumentState){return this.translate.instant('DUTY_DOC.STATE_'+doc.archiveState);}
}
