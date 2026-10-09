import { CommonModule } from '@angular/common';
import { HttpClient, HttpContext, HttpDownloadProgressEvent, HttpEventType, HttpResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { ApiResponse } from '../../core/models/api-response.model';
import { SUPPRESS_ERROR_TOAST, SUPPRESS_FORBIDDEN_REDIRECT } from '../../core/http/http-context.tokens';
import { environment } from '../../../environments/environment';
import { ImportPageComponent } from './import-page.component';
import { StorageApiService } from './storage-api.service';
import { StorageContext } from './storage.models';
import { ArchiveOperations, VisitArchiveApiService } from './visit-archive-api.service';

interface Delegation { id:number; granteeUserId:string; startsAt:string; expiresAt?:string; revokedAt?:string; reason:string; rowVersion:string; }
interface SetupFolder { key:string; name:string; parentKey:string; state:string; note?:string; }
interface SetupTeacher { teacherId:number; name:string; state:string; note?:string; }
interface SetupRecoveryIssue { key:string; name:string; folderCount:number; fileCount:number; operationCount:number; warning:string; }
interface SetupPlan { folders:SetupFolder[]; teachers:SetupTeacher[]; archiveEnabled:boolean; archiveCapabilityReady:boolean; recoveryIssues?:SetupRecoveryIssue[]; }
interface SetupResult { plan:SetupPlan; created:number; linked:number; alreadyPresent:number; warnings:string[]; }
interface SetupProgress { stage:string; label:string; completed:number; total:number; }
type SetupStreamMessage = { kind:'progress'; progress:SetupProgress } | { kind:'result'; result:SetupResult } | { kind:'error'; message:string };
interface ArchiveActivation { enabled:boolean; }

@Component({selector:'app-storage-admin',standalone:true,imports:[CommonModule,FormsModule,RouterLink,ImportPageComponent],templateUrl:'./storage-admin-page.component.html',styleUrls:['./storage-admin-page.component.css']})
export class StorageAdminPageComponent implements OnInit,OnDestroy {
  private readonly api=inject(StorageApiService); private readonly archive=inject(VisitArchiveApiService); private readonly http=inject(HttpClient);
  private readonly route=inject(ActivatedRoute); private readonly router=inject(Router); private readonly destroyed=new Subject<void>();
  readonly tabs=[{id:'drive',label:'Drive والجاهزية',icon:'pi-cloud'},{id:'setup',label:'إنشاء المكتبة',icon:'pi-folder-plus'},{id:'teachers',label:'مجلدات المعلمين',icon:'pi-users'},{id:'delegations',label:'التفويض',icon:'pi-shield'},{id:'imports',label:'الاستيراد التاريخي',icon:'pi-history'}];
  readonly openedTabs=new Set<string>(['drive']);
  private operationsRequest=0;
  tab='drive'; context?:StorageContext; operations?:ArchiveOperations; operationsError=''; error=''; loading=false;
  setup?:SetupPlan; setupLoading=false; setupSaving=false; setupError=''; setupMessage=''; setupWarnings:string[]=[];
  recoveryDialog=false; recoveryAccepted=false; private pendingSetup={folders:true,teachers:true};
  setupProgressVisible=false; setupProgressState:'running'|'success'|'error'='running';
  setupProgress:SetupProgress={stage:'scan',label:'جارٍ فحص اتصال Drive والمجلدات المرتبطة…',completed:0,total:1};
  readonly setupStages=[{id:'scan',label:'فحص Drive',hint:'التحقق من اتصال المدرسة والمجلدات المرتبطة',icon:'pi-cloud'},{id:'folders',label:'بنية المكتبة',hint:'إنشاء الناقص وربط المجلدات الموجودة',icon:'pi-folder-open'},{id:'teachers',label:'ملفات المعلمين',hint:'فحص وربط ملف كل معلم',icon:'pi-users'},{id:'verify',label:'التحقق النهائي',hint:'مراجعة النتيجة وحفظ الربط',icon:'pi-check-circle'}];
  private lastSetupSelection={folders:true,teachers:true,recoveryKeys:[] as string[]};
  archiveStatus?:boolean; archiveStatusLoading=false; archiveStatusError='';
  delegations:Delegation[]=[]; delegationsError=''; delegationsLoading=false; delegationsLoaded=false; canDelegate=false; saving=false; granteeUserId=''; expiresAt=''; reason=''; revokeTarget?:Delegation; revokeReason='';
  get connectionText(){return this.context?.connectionState==='Connected'?'المكتبة متصلة بحساب المدرسة وفق صلاحية الخادم.':'تحتاج مكتبة المدرسة إلى متابعة حالة الاتصال أو التهيئة.';}
  ngOnInit(){this.route.queryParamMap.pipe(takeUntil(this.destroyed)).subscribe(params=>{const value=params.get('tab');this.tab=this.tabs.some(item=>item.id===value)?value!:'drive';this.openedTabs.add(this.tab);if(this.tab==='delegations'&&!this.delegationsLoaded)this.loadDelegations();});this.load();}
  load(refresh=false){this.error='';this.loading=!this.context;this.api.contextInfo(false,refresh,false).pipe(takeUntil(this.destroyed)).subscribe({next:context=>{this.loading=false;this.context=context;if(!context.canManage){this.router.navigateByUrl('/unauthorized');return;}this.loadOperations();this.loadSetup();this.loadArchiveStatus();},error:e=>{this.loading=false;this.error=e?.error?.message||'تعذّر تحميل إعدادات المساحة.';}});}
  loadOperations(){const request=++this.operationsRequest;this.operationsError='';this.archive.operationsStatus().pipe(takeUntil(this.destroyed)).subscribe({next:value=>{if(request===this.operationsRequest)this.operations=value;},error:()=>{if(request===this.operationsRequest)this.operationsError='تعذّر قراءة حالة أرشفة الزيارات.';}});}
  private delegationUrl(){return `${environment.apiUrl}/api/v1/storage/delegations`;}
  private setupUrl(){return `${environment.apiUrl}/api/v1/storage/setup`;}
  private options(){return {context:new HttpContext().set(SUPPRESS_ERROR_TOAST,true).set(SUPPRESS_FORBIDDEN_REDIRECT,true)};}
  loadArchiveStatus(){if(this.archiveStatusLoading)return;this.archiveStatusLoading=true;this.archiveStatusError='';this.http.get<ApiResponse<ArchiveActivation>>(this.setupUrl()+'/visit-archive',this.options()).pipe(takeUntil(this.destroyed)).subscribe({next:r=>{this.archiveStatusLoading=false;this.archiveStatus=r.data?.enabled;},error:e=>{this.archiveStatusLoading=false;this.archiveStatusError=e?.error?.message||'تعذّر قراءة حالة الأرشفة.';}});}
  loadSetup(){if(this.setupLoading)return;this.setupLoading=true;this.setupError='';this.http.get<ApiResponse<SetupPlan>>(this.setupUrl(),this.options()).pipe(takeUntil(this.destroyed)).subscribe({next:r=>{this.setupLoading=false;this.setup=r.data??undefined;},error:e=>{this.setupLoading=false;this.setupError=e?.error?.message||'تعذّر فحص بنية Drive. تحقق من الاتصال ثم أعد المحاولة.';}});}
  get selectedRecoveryIssues(){return (this.setup?.recoveryIssues??[]).filter(issue=>
    issue.key.startsWith('teacher:') ? this.pendingSetup.teachers : this.pendingSetup.folders);}
  startSetup(folders:boolean,teachers:boolean){
    if(this.setupSaving)return;
    this.pendingSetup={folders,teachers};this.recoveryAccepted=false;
    if(this.selectedRecoveryIssues.length){this.recoveryDialog=true;return;}
    this.applySetup(folders,teachers);
  }
  confirmRecovery(){
    if(!this.recoveryAccepted)return;
    const keys=this.selectedRecoveryIssues.map(issue=>issue.key);
    this.recoveryDialog=false;
    this.applySetup(this.pendingSetup.folders,this.pendingSetup.teachers,keys);
  }
  applySetup(folders:boolean,teachers:boolean,recoveryKeys:string[]=[]){
    if(this.setupSaving)return;
    this.lastSetupSelection={folders,teachers,recoveryKeys};
    this.setupSaving=true;this.setupProgressVisible=true;this.setupProgressState='running';
    this.setupProgress={stage:'scan',label:'جارٍ فحص اتصال Drive والمجلدات المرتبطة…',completed:0,total:1};
    this.setupMessage='';this.setupError='';this.setupWarnings=[];
    let consumed=0;
    let pending='';
    let finished=false;
    const accept=(text:string)=>{
      if(text.length<consumed){consumed=0;pending='';}
      pending+=text.slice(consumed);consumed=text.length;
      let newline=pending.indexOf('\n');
      while(newline>=0){
        const line=pending.slice(0,newline).trim();pending=pending.slice(newline+1);
        if(line){
          try{
            const message=JSON.parse(line) as SetupStreamMessage;
            if(message.kind==='progress')this.setupProgress=message.progress;
            else if(message.kind==='result'){
              finished=true;this.setupSaving=false;this.setupProgressState='success';
              const result=message.result;
              this.setup=result.plan;this.setupWarnings=result.warnings;
              this.setupMessage=`تم إنشاء ${result.created} مجلد، وربط ${result.linked} عنصر، ووجدنا ${result.alreadyPresent} موجودًا مسبقًا.`;
              this.api.invalidateContext();this.loadOperations();
            }else if(message.kind==='error'){
              finished=true;this.setupSaving=false;this.setupProgressState='error';this.setupError=message.message;
            }
          }catch{this.setupSaving=false;this.setupProgressState='error';this.setupError='تعذّر قراءة تقدم العملية. افحص حالة Drive قبل إعادة المحاولة.';finished=true;}
        }
        newline=pending.indexOf('\n');
      }
    };
    this.http.request('POST',this.setupUrl()+'/stream',{body:{folders,teachers,recoveryKeys},...this.options(),observe:'events',responseType:'text',reportProgress:true})
      .pipe(takeUntil(this.destroyed)).subscribe({
        next:event=>{
          if(event.type===HttpEventType.DownloadProgress)accept((event as HttpDownloadProgressEvent).partialText??'');
          if(event instanceof HttpResponse){accept(event.body??'');if(!finished){this.setupSaving=false;this.setupProgressState='error';this.setupError='انتهى الاتصال قبل وصول نتيجة إعداد المكتبة. افحص Drive ثم أعد المحاولة.';}}
        },
        error:e=>{this.setupSaving=false;this.setupProgressState='error';this.setupError=e?.error?.message||'تعذّر إعداد المجلدات. أعد فحص Drive قبل المحاولة مجددًا.';}
      });
  }
  get setupProgressPercent(){return Math.min(100,Math.max(0,Math.round(100*this.setupProgress.completed/Math.max(1,this.setupProgress.total))));}
  get setupStageIndex(){const stage=this.setupProgress.stage==='complete'?'verify':this.setupProgress.stage;return Math.max(0,this.setupStages.findIndex(item=>item.id===stage));}
  setupStageState(index:number){const id=this.setupStages[index].id;if((id==='folders'&&!this.lastSetupSelection.folders)||(id==='teachers'&&!this.lastSetupSelection.teachers))return 'skipped';if(this.setupProgressState==='success')return 'done';if(index<this.setupStageIndex)return 'done';if(index===this.setupStageIndex)return this.setupProgressState==='error'?'error':'active';return 'pending';}
  retrySetup(){this.applySetup(this.lastSetupSelection.folders,this.lastSetupSelection.teachers,this.lastSetupSelection.recoveryKeys);}
  get archiveEnabled(){return this.archiveStatus??false;}
  setArchive(enabled:boolean){if(this.setupSaving||this.archiveStatus===undefined)return;this.setupSaving=true;this.setupError='';this.setupMessage='';this.http.put<ApiResponse<ArchiveActivation>>(this.setupUrl()+'/visit-archive',{enabled},this.options()).pipe(takeUntil(this.destroyed)).subscribe({next:r=>{this.setupSaving=false;if(r.data){this.archiveStatus=r.data.enabled;if(this.setup)this.setup={...this.setup,archiveEnabled:r.data.enabled,folders:enabled?this.setup.folders.map(folder=>folder.key==='archive'?{...folder,state:'Exists'}:folder):this.setup.folders};if(this.operations)this.operations={...this.operations,schoolEnabled:r.data.enabled,externalWritesEnabled:r.data.enabled,ready:r.data.enabled};}this.setupMessage=enabled?'تم تفعيل أرشفة زيارات هذه المدرسة.':'تم إيقاف أرشفة زيارات هذه المدرسة.';this.loadOperations();},error:e=>{this.setupSaving=false;this.setupError=e?.error?.message||'تعذّر تغيير حالة الأرشفة.';this.loadArchiveStatus();}});}
  get missingFolderCount(){return this.setup?.folders.filter(x=>x.state==='Missing'||x.state==='ParentPending'||x.state==='NeedsConfirmation').length||0;}
  get existingFolderCount(){return this.setup?.folders.filter(x=>x.state==='Exists').length||0;}
  get missingTeacherCount(){return this.setup?.teachers.filter(x=>x.state==='Missing'||x.state==='Exists'||x.state==='NeedsConfirmation'||x.state==='InvalidGrant'||x.state==='NeedsMove').length||0;}
  loadDelegations(refresh=false){if(this.delegationsLoaded&&!refresh)return;this.delegationsLoading=true;this.delegationsError='';this.http.get<ApiResponse<Delegation[]>>(this.delegationUrl(),this.options()).pipe(takeUntil(this.destroyed)).subscribe({next:response=>{this.delegationsLoading=false;this.delegationsLoaded=true;this.delegations=response.data||[];this.canDelegate=true;},error:e=>{this.delegationsLoading=false;if(e?.status===401||e?.status===403){this.delegations=[];this.canDelegate=false;this.delegationsLoaded=false;}this.delegationsError=e?.status===403?'إدارة التفويضات متاحة لمدير المدرسة المخوّل فقط.':e?.error?.message||'تعذّر قراءة التفويضات.';}});}
  grant(){if(this.saving||!this.granteeUserId.trim()||!this.reason.trim())return;this.saving=true;this.delegationsError='';const body={granteeUserId:this.granteeUserId.trim(),startsAt:new Date().toISOString(),expiresAt:this.expiresAt?new Date(this.expiresAt).toISOString():null,reason:this.reason.trim()};this.http.post<ApiResponse<Delegation>>(this.delegationUrl(),body,this.options()).pipe(takeUntil(this.destroyed)).subscribe({next:()=>{this.saving=false;this.granteeUserId='';this.expiresAt='';this.reason='';this.loadDelegations(true);},error:e=>{this.saving=false;this.delegationsError=e?.error?.message||'تعذّر منح التفويض.';}});}
  revoke(){const item=this.revokeTarget;if(!item||!this.revokeReason.trim()||this.saving)return;this.saving=true;this.delegationsError='';this.http.request<ApiResponse<Delegation>>('DELETE',`${this.delegationUrl()}/${item.id}`,{body:{reason:this.revokeReason.trim(),rowVersion:item.rowVersion},...this.options()}).pipe(takeUntil(this.destroyed)).subscribe({next:()=>{this.saving=false;this.revokeTarget=undefined;this.revokeReason='';this.loadDelegations(true);},error:e=>{this.saving=false;this.delegationsError=e?.error?.message||'تعذّر سحب التفويض.';}});}
  ngOnDestroy(){this.destroyed.next();this.destroyed.complete();}
}
