import { TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { Subject, of } from 'rxjs';
import { ClassroomCatalog, ClassroomCatalogService, ClassroomOption } from '../../../core/services/classroom-catalog.service';
import { ClassroomPickerComponent } from './classroom-picker.component';

describe('Contextual classroom picker', () => {
  let api: jasmine.SpyObj<ClassroomCatalogService>;
  const data: ClassroomCatalog = {context:{stage:'Primary',isManager:false,isMember:true,years:[]},hasMore:false,
    items:[{id:101,label:'Retained inactive',stage:'Primary',gradeLevel:1,isActive:false},
      {id:102,label:'Other inactive',stage:'Primary',gradeLevel:1,isActive:false}] as ClassroomOption[]};
  beforeEach(async () => {
    api=jasmine.createSpyObj('catalog',['all','gradeLabel']);api.all.and.returnValue(of(data));api.gradeLabel.and.returnValue('Grade');
    await TestBed.configureTestingModule({imports:[ClassroomPickerComponent,NoopAnimationsModule],providers:[{provide:ClassroomCatalogService,useValue:api}]}).compileComponents();
  });
  it('loads the visit date context and retains only the already selected inactive option', () => {
    const c=TestBed.createComponent(ClassroomPickerComponent).componentInstance;
    c.onDate='2026-06-01';c.writeValue(101);c.ngOnInit();
    expect(api.all).toHaveBeenCalledWith(undefined,'2026-06-01');
    expect(c.groups[0].items[0].disabled).toBeFalse();expect(c.groups[0].items[1].disabled).toBeTrue();
  });
  it('rechecks a saved ID even when another visit has the same date', async () => {
    const c=TestBed.createComponent(ClassroomPickerComponent).componentInstance;
    const ready:boolean[]=[];c.ready.subscribe(v=>ready.push(v));c.ngOnInit();
    c.writeValue(999);await Promise.resolve();expect(ready.at(-1)).toBeFalse();
    c.writeValue(101);await Promise.resolve();expect(ready.at(-1)).toBeTrue();
  });
  it('does not lose the selection on error or let an earlier year overwrite the current lookup', () => {
    const old=new Subject<ClassroomCatalog>(),current=new Subject<ClassroomCatalog>();api.all.and.returnValues(old,current);
    const c=TestBed.createComponent(ClassroomPickerComponent).componentInstance;
    c.writeValue(101);c.ngOnInit();c.academicYearId=2;c.ngOnChanges();
    current.error({status:503});old.next(data);
    expect(c.value).toBe(101);expect(c.error).not.toBe('');expect(c.options).toEqual([]);
    expect(api.all).toHaveBeenCalledWith(2,undefined);
  });
});
