import { of } from 'rxjs';
import { ScarfService } from '../../services/scarf';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SummerCollection } from './summer-collection';

describe('SummerCollection', () => {
  let component: SummerCollection;
  let fixture: ComponentFixture<SummerCollection>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [{ provide: ScarfService, useValue: { getScarves: () => of([]) } }],
      imports: [SummerCollection],
    }).compileComponents();

    fixture = TestBed.createComponent(SummerCollection);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
