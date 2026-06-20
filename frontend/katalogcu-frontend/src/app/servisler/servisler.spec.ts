import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ServislerComponent } from './servisler';

describe('Servisler', () => {
  let component: ServislerComponent;
  let fixture: ComponentFixture<ServislerComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ServislerComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ServislerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
