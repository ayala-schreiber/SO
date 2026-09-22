import { of } from 'rxjs';
import { ScarfService } from '../../services/scarf';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CottonScarves } from './cotton-scarves';

describe('CottonScarves', () => {
  let component: CottonScarves;
  let fixture: ComponentFixture<CottonScarves>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [{ provide: ScarfService, useValue: { getScarves: () => of([]) } }],
      imports: [CottonScarves],
    }).compileComponents();

    fixture = TestBed.createComponent(CottonScarves);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
