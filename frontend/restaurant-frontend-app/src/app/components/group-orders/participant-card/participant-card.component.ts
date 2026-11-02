import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Participant } from '@app/models/group-orders/participant.model';

@Component({
  selector: 'app-participant-card',
  templateUrl: './participant-card.component.html',
  styleUrl: './participant-card.component.scss',
  imports: [CurrencyPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class ParticipantCardComponent {
  // що картка отримує ззовні — як параметр функції
  readonly participant = input.required<Participant>();
  readonly isMe = input(false);

  // рахується саме, коли змінюється participant
  readonly title = computed(() => {
    const person = this.participant();
    if (person.type === 'Owner' && this.isMe()) {
      return 'My Pre-Order';
    }

    return person.type === 'Owner' ? `Owner: ${person.name}` : `Guest: ${person.name}`;
  });
}
