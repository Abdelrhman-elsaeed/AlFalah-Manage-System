import { TestBed } from '@angular/core/testing';
import { OrganizationSectionComponent } from './organization-section.component';

describe('OrganizationSectionComponent', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      imports: [OrganizationSectionComponent]
    })
  );

  it('includes every employee from the supplied spreadsheet', () => {
    const fixture = TestBed.createComponent(OrganizationSectionComponent);
    const component = fixture.componentInstance;

    const employeeCount =
      1 + component.deputies.length + component.departments.reduce((total, item) => total + item.members.length, 0);

    expect(employeeCount).toBe(31);
  });

  it('expands the chart and individual departments independently', () => {
    spyOn(window, 'matchMedia').and.returnValue({ matches: true } as MediaQueryList);
    const fixture = TestBed.createComponent(OrganizationSectionComponent);
    fixture.detectChanges();

    const chartButton = fixture.nativeElement.querySelector('.reveal-button') as HTMLButtonElement;
    chartButton.click();
    fixture.detectChanges();

    expect(chartButton.getAttribute('aria-expanded')).toBe('true');
    expect(fixture.componentInstance.chartOpen()).toBeTrue();

    const firstDepartment = fixture.nativeElement.querySelector('.department-toggle') as HTMLButtonElement;
    firstDepartment.click();
    fixture.detectChanges();

    expect(firstDepartment.getAttribute('aria-expanded')).toBe('true');
    expect(fixture.nativeElement.querySelectorAll('.is-expanded .member-card').length).toBe(4);
  });

  it('matches the landing-page heading scale and has no logo watermark', () => {
    spyOn(window, 'matchMedia').and.returnValue({ matches: true } as MediaQueryList);
    const fixture = TestBed.createComponent(OrganizationSectionComponent);
    fixture.detectChanges();

    const heading = fixture.nativeElement.querySelector('h2') as HTMLHeadingElement;
    const section = fixture.nativeElement.querySelector('.organization') as HTMLElement;
    const headingStyle = getComputedStyle(heading);
    const watermarkStyle = getComputedStyle(section, '::before');

    expect(headingStyle.textAlign).toBe('center');
    expect(Number.parseFloat(headingStyle.fontSize)).toBeLessThanOrEqual(42);
    expect(watermarkStyle.backgroundImage).toBe('none');
  });

  it('merges both deputy connectors into one central departments node', () => {
    spyOn(window, 'matchMedia').and.returnValue({ matches: true } as MediaQueryList);
    const fixture = TestBed.createComponent(OrganizationSectionComponent);
    fixture.detectChanges();

    const connector = fixture.nativeElement.querySelector(
      '.departments-connector .connector-line'
    ) as SVGPathElement;
    const path = connector.getAttribute('d') ?? '';

    expect(path).toContain('M300 0');
    expect(path).toContain('M700 0');
    expect(path).toContain('L500 76');
    expect(path).not.toContain('H945');
  });

  it('keeps the departments card below the connector node without overlap', () => {
    spyOn(window, 'matchMedia').and.returnValue({ matches: true } as MediaQueryList);
    const fixture = TestBed.createComponent(OrganizationSectionComponent);
    fixture.detectChanges();

    const heading = fixture.nativeElement.querySelector('.departments-heading') as HTMLElement;

    expect(Number.parseFloat(getComputedStyle(heading).marginTop)).toBeGreaterThanOrEqual(0);
  });
});
