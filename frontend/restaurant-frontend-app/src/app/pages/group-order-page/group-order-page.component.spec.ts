// Рівень 1 · верстка сторінки на статичних даних (GROUP_ORDER_PREVIEW).
// Запуск: npm run test:hw
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { GROUP_ORDER_PREVIEW } from '@app/shared/mocks/group-orders/group-order.preview';
import { GroupOrderPageComponent } from './group-order-page.component';

describe('Рівень 1 · верстка сторінки', () => {
  let element: HTMLElement;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(GroupOrderPageComponent);
    fixture.detectChanges();
    element = fixture.nativeElement as HTMLElement;
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('один <main> і один <h1> «Group Pre-Order»', () => {
    expect(element.querySelectorAll('main').length).toBe(1);
    expect(element.querySelectorAll('h1').length).toBe(1);
    expect(element.querySelector('h1')?.textContent).toContain('Group Pre-Order');
  });

  it('шапка показує адресу з invite, дату і час через date pipe та кількість/місткість гостей', () => {
    const text = element.textContent ?? '';
    const { invite } = GROUP_ORDER_PREVIEW;
    const times = Array.from(element.querySelectorAll('time'));
    expect(text).toContain(invite.LocationAddress);
    expect(text).toContain(`${GROUP_ORDER_PREVIEW.sessions.participants.length} of ${invite.MaxGuestCount} guests`);
    expect(times.map((time) => time.textContent?.trim())).toEqual(['Oct 21, 2026', '16:00', '17:30']);
    expect(times.map((time) => time.getAttribute('datetime'))).toEqual([invite.StartTime, invite.StartTime, invite.EndTime]);
  });

  it('учасники відображені у <ul>/<li>, а картки — семантичними <article>', () => {
    const cards = Array.from(element.querySelectorAll('app-participant-card'));
    expect(cards.length).toBe(GROUP_ORDER_PREVIEW.sessions.participants.length);
    expect(element.querySelectorAll('app-participant-card article').length).toBe(cards.length);
    expect(cards.every((card) => card.parentElement?.tagName === 'LI' && card.parentElement.parentElement?.tagName === 'UL')).toBeTrue();
  });

  it('статус кошика видно текстом', () => {
    expect(element.textContent).toContain('Submitted');
    expect(element.textContent).toContain('Still choosing');
  });

  it('Grand Total відображає sessions.total через currency pipe', () => {
    const total = Array.from(element.querySelectorAll('footer')).find((footer) => footer.textContent?.includes('Grand Total'));
    const expected = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(GROUP_ORDER_PREVIEW.sessions.total);
    expect(total?.textContent).toContain('Grand Total');
    expect(total?.textContent).toContain(expected);
  });

  it('статична сторінка не надсилає HTTP-запитів', () => {
    expect(httpMock.match(() => true)).toEqual([]);
  });
});
