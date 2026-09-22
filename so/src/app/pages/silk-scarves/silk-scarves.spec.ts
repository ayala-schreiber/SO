import { of } from 'rxjs';
import { ScarfService } from '../../services/scarf';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SilkScarvesComponent } from './silk-scarves';

describe('SilkScarvesComponent', () => {
  let component: SilkScarvesComponent;
  let fixture: ComponentFixture<SilkScarvesComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [{ provide: ScarfService, useValue: { getScarves: () => of([]) } }],
      imports: [SilkScarvesComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(SilkScarvesComponent);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
