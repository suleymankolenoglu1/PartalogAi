import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideStandaloneComponentTestDeps } from '../../../testing/standalone-component-test-providers';
import { Customer } from '../../core/services/customer.service';
import { CustomersComponent } from './customers';

describe('CustomersComponent', () => {
  let component: CustomersComponent;
  let fixture: ComponentFixture<CustomersComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CustomersComponent],
      providers: provideStandaloneComponentTestDeps(),
    }).compileComponents();

    fixture = TestBed.createComponent(CustomersComponent);
    component = fixture.componentInstance;
  });

  it('calculates portal access summary counts', () => {
    component.publicToken = 'portal-token';
    component.customers = [
      buildCustomer({ id: 'active', hasPassword: true, status: 'active' }),
      buildCustomer({ id: 'pending', hasPassword: false, status: 'active' }),
      buildCustomer({ id: 'inactive', hasPassword: true, status: 'inactive' }),
    ];

    expect(component.activeCustomerCount).toBe(1);
    expect(component.pendingCustomerCount).toBe(1);
    expect(component.inactiveCustomerCount).toBe(1);
    expect(component.invitableCustomerCount).toBe(2);
  });

  it('allows invite copy only for active customers after a portal link exists', () => {
    const activeCustomer = buildCustomer({ status: 'active' });
    const inactiveCustomer = buildCustomer({ status: 'inactive' });

    component.publicToken = null;
    expect(component.canCopyInvite(activeCustomer)).toBeFalse();

    component.publicToken = 'portal-token';
    expect(component.canCopyInvite(activeCustomer)).toBeTrue();
    expect(component.canCopyInvite(inactiveCustomer)).toBeFalse();
  });
});

function buildCustomer(overrides: Partial<Customer> = {}): Customer {
  return {
    id: overrides.id ?? 'customer-id',
    name: overrides.name ?? 'Portal Customer',
    company: overrides.company ?? 'Customer Co',
    email: overrides.email ?? 'customer@example.test',
    phone: overrides.phone ?? '905551112233',
    orderCount: overrides.orderCount ?? 0,
    totalSpent: overrides.totalSpent ?? 0,
    lastVisitDate: overrides.lastVisitDate ?? '2026-06-24T00:00:00Z',
    lastOrderDate: overrides.lastOrderDate ?? null,
    lastLoginDate: overrides.lastLoginDate ?? null,
    lastActivityDate: overrides.lastActivityDate ?? '2026-06-24T00:00:00Z',
    hasPassword: overrides.hasPassword ?? false,
    status: overrides.status ?? 'active',
    note: overrides.note ?? null,
    createdDate: overrides.createdDate ?? '2026-06-24T00:00:00Z',
  };
}
