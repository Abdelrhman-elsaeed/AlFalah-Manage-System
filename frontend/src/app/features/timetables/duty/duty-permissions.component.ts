import { CommonModule } from '@angular/common';
import { Component,DestroyRef,OnInit,inject,signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { TranslateModule,TranslateService } from '@ngx-translate/core';
import { DialogModule } from 'primeng/dialog';
import { SchoolDutyService,DutyAdministratorAccount } from '../../../core/services/school-duty.service';
import { ClearableSelectComponent } from '../../../shared/components/clearable-select/clearable-select.component';
import { DutyErrorDirective } from './duty-error.directive';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';

@Component({selector:'app-duty-permissions',standalone:true,imports:[CommonModule,FormsModule,TranslateModule,DialogModule,ClearableSelectComponent,DutyErrorDirective],templateUrl:'./duty-permissions.component.html',styleUrls:['./duty-permissions.component.css']})
export class DutyPermissionsComponent implements OnInit {
 private readonly api=inject(SchoolDutyService);private readonly translate=inject(TranslateService);private readonly destroy=inject(DestroyRef);
 readonly accounts=signal<DutyAdministratorAccount[]>([]);readonly busy=signal(false);readonly error=signal('');readonly success=signal('');
 selected:string|null=null;search='';page=0;pending:DutyAdministratorAccount|null=null;confirm=false;private origin:HTMLElement|null=null;
 ngOnInit(){this.load();}
 t(key:string){return this.translate.instant('DUTY_ACCESS.'+key);}
 role(role:string){const key='DUTY_ACCESS.ROLE_'+role;const label=this.translate.instant(key);return label===key?role:label;}
 get groups(){const groups=new Map<string,any[]>();for(const account of this.accounts().filter(x=>x.eligible&&!x.inherited&&!x.enabled)){
  const role=account.roles.includes('Moderator')?'Moderator':account.roles[0]||'Other';const items=groups.get(role)||[];items.push({label:account.username?`${account.name} · ${account.username}`:account.name,value:account.userId});groups.set(role,items);
 }return [...groups].map(([role,items])=>({label:this.role(role),items}));}
 get administrators(){const q=this.search.trim().toLocaleLowerCase();return this.accounts().filter(x=>(x.enabled||x.inherited)&&(!q||x.name.toLocaleLowerCase().includes(q)));}
 get pages(){return Math.max(1,Math.ceil(this.administrators.length/10));}
 get displayed(){const page=Math.min(this.page,this.pages-1);return this.administrators.slice(page*10,page*10+10);}
 move(delta:number){this.page=Math.max(0,Math.min(this.pages-1,this.page+delta));}
 load(){if(this.busy())return;this.busy.set(true);this.api.administrators().pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r=>{this.busy.set(false);if(r.isSuccess&&r.data)this.accounts.set(r.data);else this.error.set(r.message||this.t('ERROR'));},error:e=>{this.busy.set(false);if(e.status===403)this.accounts.set([]);this.error.set(extractHttpErrorMessage(e)||this.t('ERROR'));}});}
 grant(){const account=this.accounts().find(x=>x.userId===this.selected);if(account)this.save(account,true);}
 revoke(account:DutyAdministratorAccount){this.origin=document.activeElement as HTMLElement;this.pending=account;this.confirm=true;}
 restoreFocus(){this.origin?.focus();}
 save(account:DutyAdministratorAccount,enabled:boolean){if(this.busy())return;this.busy.set(true);this.error.set('');this.success.set('');this.api.changeAdministrator(account.userId,enabled,account.version).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r=>{this.busy.set(false);if(r.isSuccess&&r.data){this.accounts.set(r.data);this.selected=null;this.confirm=false;this.pending=null;this.success.set(this.t(enabled?'GRANTED':'REVOKED'));}else this.error.set(r.message||this.t('ERROR'));},error:e=>{this.busy.set(false);if(e.status===403){this.accounts.set([]);this.confirm=false;}this.error.set(extractHttpErrorMessage(e)||this.t('ERROR'));}});}
}
