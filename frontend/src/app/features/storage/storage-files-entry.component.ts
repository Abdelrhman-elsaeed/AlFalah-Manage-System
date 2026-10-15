import { CommonModule } from '@angular/common';
import { Component, DestroyRef, ElementRef, HostListener, ViewChild, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpContext } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api-response.model';
import { SUPPRESS_ERROR_TOAST, SUPPRESS_FORBIDDEN_REDIRECT } from '../../core/http/http-context.tokens';
import { StorageDriveBrowserComponent } from './storage-drive-browser.component';
import { StoragePageComponent } from './storage-page.component';

export interface LibraryMapNode { id:number; parentId:number|null; name:string; files:number; state:string; }
export interface LibraryMapSpace { key:string; name:string; state:string; canOpen:boolean; folderId:number|null; browseHandle?:string|null; }
export interface LibraryMap { schoolName:string; year?:string; checkedAt:string; spaces:LibraryMapSpace[]; nodes:LibraryMapNode[]; registeredFiles:number; }

const copy = {
 ar: {eyebrow:'مساحة المدرسة',title:'كل ملفات مدرستك، في مكان واحد.',hint:'افتح المساحة المناسبة أو استعرض خريطة المكتبة في عرض مخصص للجان.',folders:'المجلدات',map:'الخريطة البصرية',refresh:'تحديث',loading:'نراجع اتصال المدرسة وبنية المكتبة…',retry:'إعادة المحاولة',error:'تعذّر عرض مكتبة المدرسة. تحقق من الاتصال ثم أعد المحاولة.',denied:'لم تعد هذه المساحة متاحة لحسابك.',back:'كل ملفات المدرسة',full:'عرض ملء الشاشة',exit:'إنهاء ملء الشاشة',search:'ابحث باسم المجال أو المعيار',treeTitle:'خريطة مكتبة المدرسة',treeHint:'المجالات والمعايير، بروابطها وملفاتها المسجلة. افتح أي بطاقة لمتابعة محتواها.',registered:'ملف مسجل',standards:'معايير',domains:'مجالات',open:'فتح المساحة',schoolRoot:'جذر ملفات المدرسة',empty:'لم تُنشأ مجالات المكتبة بعد.',setup:'إنشاء المكتبة',updated:'آخر فحص',unavailable:'المجلد يحتاج مراجعة',missing:'لم يُنشأ بعد',conflict:'أسماء متعارضة',permission:'صلاحية مستقلة مطلوبة',collapse:'طي المجال',expand:'عرض المعايير',noResults:'لا توجد نتائج لهذا البحث.',note:'أعداد الملفات من السجل المحفوظ، ولا تعني اعتماد الشواهد. الأرشيفان مستقلان عن التقويم.',library:'المجالات والمعايير وملفات المدرسة العامة',teachers:'ملف مستقل لكل معلم وصلاحياته',duty:'خطابات التعهد والتقارير اليومية الموقعة',visits:'نسخ الزيارات المعتمدة وسجل الأرشفة',available:'متاح',stateHint:'افتح إعداد المكتبة لمراجعة الربط.',portal:'منظومة ملفات المدرسة',viewFolder:'عرض الملفات',protected:'أرشيف محمي',tracked:'بنية موثقة',nodes:'مجلدات البنية',noSample:'بيانات مدرستك الحالية',overview:'عرض المكتبة',unindexed:'يحتاج ربطًا بالمكتبة'},
 en: {eyebrow:'School workspace',title:'Your school’s files. One clear view.',hint:'Open a workspace or present the library map during committee reviews.',folders:'Folders',map:'Visual map',refresh:'Refresh',loading:'Checking school connectivity and library structure…',retry:'Try again',error:'The school library could not be loaded. Check connectivity and try again.',denied:'This workspace is no longer available to your account.',back:'All school files',full:'Full screen',exit:'Exit full screen',search:'Search domains and standards',treeTitle:'School library map',treeHint:'Domains, standards and registered files. Open a card to explore its contents.',registered:'registered files',standards:'standards',domains:'domains',open:'Open workspace',schoolRoot:'School file root',empty:'Library domains have not been created yet.',setup:'Set up library',updated:'Last checked',unavailable:'Folder needs review',missing:'Not created yet',conflict:'Conflicting names',permission:'Separate permission required',collapse:'Collapse domain',expand:'Show standards',noResults:'No matches for this search.',note:'File counts come from the retained ledger and do not establish evidence approval. Archives are separate from evaluation.',library:'Domains, standards and general school files',teachers:'An independent workspace for each teacher',duty:'Signed responsibility letters and daily reports',visits:'Approved visit documents and archive history',available:'Available',stateHint:'Review the link in library setup.',portal:'School file system',viewFolder:'View files',protected:'Protected archive',tracked:'Verified structure',nodes:'structure folders',noSample:'Your current school data',overview:'Library view',unindexed:'Library link required'}
};

@Component({selector:'app-storage-files-entry',standalone:true,imports:[CommonModule,FormsModule,RouterLink,StoragePageComponent,StorageDriveBrowserComponent],
 templateUrl:'./storage-files-entry.component.html',styleUrls:['./storage-files-entry.component.css']})
export class StorageFilesEntryComponent {
 @ViewChild('presentation') presentation?:ElementRef<HTMLElement>;
 readonly route=inject(ActivatedRoute);private readonly http=inject(HttpClient);private readonly destroy=inject(DestroyRef);private readonly translate=inject(TranslateService);
 readonly data=signal<LibraryMap|null>(null);readonly loading=signal(false);readonly error=signal('');mode:'folders'|'map'='folders';search='';readonly collapsed=new Set<number>();full=false;private sequence=0;
 get en(){return this.translate.currentLang==='en';}get dir(){return this.en?'ltr':'rtl';}
 t(key:keyof typeof copy.ar){return copy[this.en?'en':'ar'][key];}
 get library(){const p=this.route.snapshot.queryParamMap;return p.has('folder')||p.has('file')||p.get('view')==='review'||p.has('requirement')||p.has('academicYearId')||p.has('search');}
 get domains(){const d=this.data();const root=d?.spaces.find(x=>x.key==='library')?.folderId;return d?.nodes.filter(x=>x.parentId===root)??[];}
 children(id:number){return this.data()?.nodes.filter(x=>x.parentId===id)??[];}
 get visibleDomains(){const q=this.search.trim().toLocaleLowerCase();return this.domains.filter(d=>!q||d.name.toLocaleLowerCase().includes(q)||this.children(d.id).some(x=>x.name.toLocaleLowerCase().includes(q)));}
 get files(){return this.data()?.registeredFiles??0;}
 get standards(){return this.domains.reduce((n,x)=>n+this.children(x.id).length,0);}
 get structured(){return this.domains.length>0&&this.domains.every(x=>/^[1-4]\s*-\s*/.test(x.name));}
 get domainLabel(){return this.structured?this.t('domains'):(this.en?'library sections':'أقسام المكتبة');}
 get standardLabel(){return this.structured?this.t('standards'):(this.en?'subfolders':'مجلدات فرعية');}
 code(node:LibraryMapNode){return /^(\d+(?:\.\d+)*)\s*-\s*/.exec(node.name)?.[1]??'';}
 name(node:LibraryMapNode){return node.name.replace(/^\d+(?:\.\d+)*\s*-\s*/,'');}
 filesIn(id:number){return (this.data()?.nodes.find(x=>x.id===id)?.files??0)+this.children(id).reduce((n,x)=>n+x.files,0);}
 domainIndex(domain:LibraryMapNode){return Math.max(0,this.domains.findIndex(x=>x.id===domain.id));}
 icon(key:string){return key==='teachers'?'pi-users':key==='duty'?'pi-sun':key==='visits'?'pi-clipboard':'pi-folder-open';}
 state(space:LibraryMapSpace){return space.state==='Available'?space.canOpen?this.t('available'):this.t('permission'):space.state==='Missing'?this.t('missing'):space.state==='Unindexed'?this.t('unindexed'):space.state==='Conflict'?this.t('conflict'):this.t('unavailable');}
 link(_:LibraryMapSpace){return '/school-manager/storage';}
 params(space:LibraryMapSpace){return space.browseHandle?{drive:space.browseHandle}:{};}
 hint(key:string){return this.t(key as 'library'|'teachers'|'duty'|'visits');}
 constructor(){this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroy)).subscribe(p=>{this.mode=p.get('display')==='map'?'map':'folders';if(!this.library&&this.mode==='map'&&!this.data()&&!this.loading())this.load();});}
 showMap(){this.mode='map';if(!this.data())this.load();}
 load(){if(this.loading())return;const request=++this.sequence;this.loading.set(true);this.error.set('');this.http.get<ApiResponse<LibraryMap>>(`${environment.apiUrl}/api/v1/storage/map`,{context:new HttpContext().set(SUPPRESS_ERROR_TOAST,true).set(SUPPRESS_FORBIDDEN_REDIRECT,true)}).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r=>{if(request!==this.sequence)return;this.loading.set(false);if(!r.isSuccess||!r.data){this.error.set(this.t('error'));return;}this.data.set(r.data);},error:e=>{if(request!==this.sequence)return;this.loading.set(false);this.data.set(null);this.error.set(e.status===401||e.status===403?this.t('denied'):this.t('error'));}});}
 toggle(id:number){if(this.collapsed.has(id))this.collapsed.delete(id);else this.collapsed.add(id);}
 async fullscreen(){try{if(document.fullscreenElement)await document.exitFullscreen();else await this.presentation?.nativeElement.requestFullscreen();this.full=!!document.fullscreenElement;}catch{this.full=false;}}
 @HostListener('document:fullscreenchange') fullscreenChanged(){this.full=document.fullscreenElement===this.presentation?.nativeElement;}
 track(_:number,node:LibraryMapNode){return node.id;}
}
