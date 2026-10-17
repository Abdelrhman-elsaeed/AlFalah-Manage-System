import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { TranslateModule } from '@ngx-translate/core';
import { routes } from '../../../app.routes';
import { SHELL_NAV_CATEGORIES } from '../../../shared/layout/shell/shell.component';
import { TimetableWorkspaceComponent } from '../timetable-workspace.component';
import { SchoolTimetableComponent } from '../school-timetable.component';
import { StandbyWorkspaceComponent } from './standby-workspace.component';
import { DailyCoverageLedgerComponent } from '../../intelligent-timetable/substitutions/daily-coverage-ledger.component';
import { legacyCoverageRegisterGuard } from '../../../core/guards/legacy-coverage-register.guard';

describe('Independent weekly standby navigation',()=>{
  it('has one guarded menu entry under Intelligent timetable and a standalone route',()=>{
    const parent=routes.flatMap(r=>r.children??[]).find(r=>r.path==='intelligent-timetable');
    const route=parent?.children?.find(r=>r.path==='standby');
    expect(route?.data?.['standbyOnly']).toBeTrue();expect(route?.canDeactivate?.length).toBe(1);
    expect(parent?.data?.['roles']).not.toContain('Instructor');
    const items=SHELL_NAV_CATEGORIES.find(c=>c.id==='intelligent-timetable')?.items.filter(i=>i.route==='/intelligent-timetable/standby');
    expect(items?.length).toBe(1);expect(items?.[0].roles).toContain('SchoolManager');expect(items?.[0].permissions).toContain('Timetable.View');
  });
  it('retains weekly edits across daily switching and guards leaving or switching during writes',()=>{
    const query=new BehaviorSubject(convertToParamMap({}));const router=jasmine.createSpyObj('Router',['navigate']);router.navigate.and.returnValue(Promise.resolve(true));
    TestBed.configureTestingModule({imports:[TranslateModule.forRoot()],providers:[{provide:ActivatedRoute,useValue:{queryParamMap:query}},{provide:Router,useValue:router}]});
    const host=TestBed.runInInjectionContext(()=>new StandbyWorkspaceComponent());let busy=false,dirty=true,dailyBusy=false;
    host.weekly={standbyBusy:()=>busy,saving:()=>false,hasPendingChanges:()=>dirty} as unknown as SchoolTimetableComponent;
    host.daily={canLeave:()=>!dailyBusy,registerOpen:false} as DailyCoverageLedgerComponent;
    const original=host.weekly;query.next(convertToParamMap({tab:'daily'}));expect(host.tab).toBe('daily');expect(host.weekly).toBe(original);expect(host.weeklyVisited&&host.dailyVisited).toBeTrue();
    const confirm=spyOn(window,'confirm').and.returnValue(false);expect(host.canLeave()).toBeFalse();expect(confirm).toHaveBeenCalledTimes(1);
    dailyBusy=true;host.select('weekly');expect(router.navigate).not.toHaveBeenCalled();expect(host.canLeave()).toBeFalse();
    dailyBusy=false;host.select('weekly');expect(router.navigate).toHaveBeenCalledTimes(1);query.next(convertToParamMap({}));expect(host.tab).toBe('weekly');
    busy=true;query.next(convertToParamMap({tab:'daily'}));expect(host.tab).toBe('weekly');dirty=false;expect(host.canLeave()).toBeFalse();
    busy=false;expect(host.canLeave()).toBeTrue();host.openRegister();expect(host.daily.registerOpen).toBeTrue();
  });
  it('opens old PDF links in the new hub while preserving lesson swap deep links',()=>{
    const router=jasmine.createSpyObj('Router',['createUrlTree']);router.createUrlTree.and.returnValue('redirect');
    TestBed.configureTestingModule({providers:[{provide:Router,useValue:router}]});
    const invoke=(params:Record<string,string>)=>TestBed.runInInjectionContext(()=>legacyCoverageRegisterGuard({queryParams:params,queryParamMap:convertToParamMap(params)} as any,{} as any));
    expect(invoke({sourceEntryId:'12',date:'2026-10-18'})).toBeTrue();expect(router.createUrlTree).not.toHaveBeenCalled();
    expect(invoke({register:'true',date:'2026-10-18'})).toBe('redirect' as any);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/intelligent-timetable/standby'],{queryParams:{register:'true',date:'2026-10-18',tab:'daily'}});
  });
  it('retains pending-edit and in-flight-operation protection on the standalone page',()=>{
    TestBed.configureTestingModule({imports:[TranslateModule.forRoot()],providers:[{provide:ActivatedRoute,useValue:{snapshot:{data:{standbyOnly:true}}}}]});
    const host=TestBed.runInInjectionContext(()=>new TimetableWorkspaceComponent());expect(host.standbyOnly).toBeTrue();
    let busy=false,dirty=false;host.lessons={standbyBusy:()=>busy,hasPendingChanges:()=>dirty} as unknown as SchoolTimetableComponent;
    const confirm=spyOn(window,'confirm').and.returnValue(false);expect(host.canLeave()).toBeTrue();expect(confirm).not.toHaveBeenCalled();
    dirty=true;expect(host.canLeave()).toBeFalse();confirm.and.returnValue(true);expect(host.canLeave()).toBeTrue();
    busy=true;expect(host.canLeave()).toBeFalse();
  });
});
