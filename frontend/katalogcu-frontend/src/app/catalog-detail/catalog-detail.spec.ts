import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideStandaloneComponentTestDeps } from '../../testing/standalone-component-test-providers';

import { CatalogDetailComponent } from './catalog-detail';

describe('CatalogDetail', () => {
  let component: CatalogDetailComponent;
  let fixture: ComponentFixture<CatalogDetailComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CatalogDetailComponent],
      providers: provideStandaloneComponentTestDeps(),
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogDetailComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
