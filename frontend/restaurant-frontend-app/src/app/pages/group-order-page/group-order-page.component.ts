import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { GROUP_ORDER_PREVIEW } from '@app/shared/mocks/group-orders/group-order.preview';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';

@Component({
  selector: 'app-group-order-page',
  imports: [CurrencyPipe, DatePipe, ParticipantCardComponent],
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent {
  // поки що статичні дані; на рівні 2 їх замінить відповідь API
  readonly participants = signal(GROUP_ORDER_PREVIEW.sessions.participants);
  readonly invite = GROUP_ORDER_PREVIEW.invite;
  readonly grandTotal = GROUP_ORDER_PREVIEW.sessions.total;
  readonly guestCount = computed(() => this.participants().length);
  readonly spansMultipleDays = this.invite.StartTime.slice(0, 10) !== this.invite.EndTime.slice(0, 10);
}
