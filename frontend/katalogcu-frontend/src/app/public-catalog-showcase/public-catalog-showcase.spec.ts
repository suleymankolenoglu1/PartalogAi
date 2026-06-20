import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PublicCatalogShowcaseComponent } from './public-catalog-showcase';

describe('PublicCatalogShowcase', () => {
  let component: PublicCatalogShowcaseComponent;
  let fixture: ComponentFixture<PublicCatalogShowcaseComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PublicCatalogShowcaseComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PublicCatalogShowcaseComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
