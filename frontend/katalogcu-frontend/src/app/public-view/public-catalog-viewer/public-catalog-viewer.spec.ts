import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideStandaloneComponentTestDeps } from '../../../testing/standalone-component-test-providers';

import { PublicCatalogViewerComponent } from './public-catalog-viewer';

describe('PublicCatalogViewer', () => {
  let component: PublicCatalogViewerComponent;
  let fixture: ComponentFixture<PublicCatalogViewerComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PublicCatalogViewerComponent],
      providers: provideStandaloneComponentTestDeps(),
    }).compileComponents();

    fixture = TestBed.createComponent(PublicCatalogViewerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
