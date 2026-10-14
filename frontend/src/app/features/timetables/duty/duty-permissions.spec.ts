import { TestBed } from '@angular/core/testing';
import { Subject,of } from 'rxjs';
import { TranslateService } from '@ngx-translate/core';
import { SchoolDutyService } from '../../../core/services/school-duty.service';
import { DutyPermissionsComponent } from './duty-permissions.component';
describe('School duty permission controls',()=>{
 let component:DutyPermissionsComponent;let api:any;
 beforeEach(()=>{api={administrators:jasmine.createSpy().and.returnValue(of({isSuccess:true,data:[]})),changeAdministrator:jasmine.createSpy()};TestBed.configureTestingModule({providers:[{provide:SchoolDutyService,useValue:api},{provide:TranslateService,useValue:{instant:(x:string)=>x}}]});component=TestBed.runInInjectionContext(()=>new DutyPermissionsComponent());});
 it('groups all eligible account roles and excludes inherited or already delegated accounts from the picker',()=>{
  component.accounts.set([{userId:'moderator',name:'Moderator',roles:['Moderator'],eligible:true,inherited:false,enabled:false,version:null},{userId:'guardian',name:'Guardian',roles:['Guardian'],eligible:true,inherited:false,enabled:false,version:null},{userId:'manager',name:'Manager',roles:['SchoolManager'],eligible:true,inherited:true,enabled:false,version:null}]);
  expect(component.groups.map(x=>x.label)).toEqual(['Moderator','Guardian']);expect(component.groups.flatMap(x=>x.items).map(x=>x.value)).toEqual(['moderator','guardian']);expect(component.administrators.length).toBe(1);
 });
 it('sends the retained version and prevents duplicate mutations while preserving selection on failure',()=>{
  const response=new Subject<any>();api.changeAdministrator.and.returnValue(response);const account={userId:'moderator',name:'Moderator',roles:['Moderator'],eligible:true,inherited:false,enabled:false,version:'2030-01-01T12:00:00+00:00'};component.accounts.set([account]);component.selected='moderator';component.grant();component.grant();expect(api.changeAdministrator).toHaveBeenCalledTimes(1);expect(api.changeAdministrator).toHaveBeenCalledWith('moderator',true,account.version);response.error({status:409,error:{message:'Conflict'}});expect(component.selected).toBe('moderator');expect(component.busy()).toBeFalse();expect(component.error()).toBe('Conflict');
 });
 it('distinguishes duplicate display names using their existing usernames and stable account values',()=>{
  component.accounts.set(['first','second'].map(user=>({userId:user,username:user,name:'Same name',roles:['Moderator'],eligible:true,inherited:false,enabled:false,version:null})));
  expect(component.groups[0].items.map(x=>x.label)).toEqual(['Same name · first','Same name · second']);expect(component.groups[0].items.map(x=>x.value)).toEqual(['first','second']);
 });
 it('revokes only after explicit confirmation and clears data on authorization loss',()=>{
  const response=new Subject<any>();api.changeAdministrator.and.returnValue(response);const account={userId:'m',name:'Moderator',roles:['Moderator'],eligible:true,inherited:false,enabled:true,version:'v'};component.accounts.set([account]);component.revoke(account);expect(api.changeAdministrator).not.toHaveBeenCalled();component.save(account,false);response.error({status:403,error:{message:'Denied'}});expect(component.accounts()).toEqual([]);expect(component.confirm).toBeFalse();
 });
});
