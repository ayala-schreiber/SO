import { of } from 'rxjs';
import { ScarfService } from '../../services/scarf';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Sale } from './sale';

describe('Sale', () => {
  let component: Sale;
  let fixture: ComponentFixture<Sale>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [{ provide: ScarfService, useValue: { getScarves: () => of([]) } }],
      imports: [Sale],
    }).compileComponents();

    fixture = TestBed.createComponent(Sale);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
