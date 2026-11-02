import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import {
  catchError,
  EMPTY,
  exhaustMap,
  forkJoin,
  interval,
  merge,
  of,
  Observable,
  Subject,
  Subscription,
  switchMap,
} from 'rxjs';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { Participant } from '@app/models/group-orders/participant.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { ConsolidatedOrder, consolidatePreOrder } from '@app/shared/helpers/consolidate-pre-order';
import { getServerErrorMessage } from '@app/shared/helpers/http-error-helpers';

const GROUP_ORDER_REFRESH_INTERVAL_MS = 15_000;

@Component({
  selector: 'app-group-order-page',
  imports: [CurrencyPipe, DatePipe, ParticipantCardComponent, ModalComponent],
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly groupOrdersService = inject(GroupOrdersService);
  private readonly toastService = inject(ToastService);
  private loadSubscription: Subscription | null = null;
  private pollSubscription: Subscription | null = null;
  private readonly manualRefreshRequests = new Subject<void>();

  readonly reservationId = signal('');
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly invite = signal<GuestInvite | null>(null);
  readonly participants = signal<Participant[]>([]);
  readonly guestSessionId = signal<string | null>(null);
  readonly groupStatus = signal<GroupStatusResponse | null>(null);
  readonly isSubmitting = signal(false);
  readonly submitDialogOpen = signal(false);
  readonly updatedAt = signal<Date | null>(null);
  readonly refreshFailed = signal(false);

  readonly consolidatedOrder = computed<ConsolidatedOrder>(() =>
    consolidatePreOrder(this.participants())
  );
  readonly grandTotal = computed(() => this.consolidatedOrder().total);
  readonly guestCount = computed(() => this.participants().length);
  readonly spansMultipleDays = computed(() => {
    const currentInvite = this.invite();
    return (
      !!currentInvite && currentInvite.StartTime.slice(0, 10) !== currentInvite.EndTime.slice(0, 10)
    );
  });
  readonly statusLabel = computed(() => {
    switch (this.groupStatus()?.status) {
      case 'Open':
        return 'Collecting pre-orders';
      case 'Submitted':
        return 'Sent to Restaurant';
      case 'Closed':
        return 'Closed';
      default:
        return '';
    }
  });
  readonly pendingParticipantCount = computed(
    () => this.participants().filter((participant) => !participant.isSubmitted).length
  );
  readonly hasNoDishes = computed(
    () =>
      this.participants().length === 0 ||
      this.participants().every((participant) => participant.items.length === 0)
  );
  readonly isReadOnly = computed(() => this.groupStatus()?.status !== 'Open');
  readonly canSubmit = computed(
    () => !this.isReadOnly() && !this.isSubmitting() && this.consolidatedOrder().items.length > 0
  );

  ngOnInit(): void {
    const paramMap$ = this.route.paramMap ?? of(this.route.snapshot.paramMap);
    paramMap$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      this.reservationId.set(params.get('reservationId') ?? '');
      this.load();
    });
    this.destroyRef.onDestroy(() => this.stopPolling());
  }

  retry(): void {
    this.load();
  }

  refreshNow(): void {
    if (this.groupStatus()?.status === 'Open') {
      this.manualRefreshRequests.next();
    }
  }

  openSubmitDialog(): void {
    if (this.canSubmit()) {
      this.submitDialogOpen.set(true);
    }
  }

  closeSubmitDialog(): void {
    if (!this.isSubmitting()) {
      this.submitDialogOpen.set(false);
    }
  }

  confirmSubmit(): void {
    const groupReservationId = this.invite()?.GroupReservationId;
    const currentReservationId = this.reservationId();
    if (!groupReservationId || !currentReservationId || !this.canSubmit()) {
      return;
    }

    this.isSubmitting.set(true);
    this.groupOrdersService
      .submit$(groupReservationId, currentReservationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.stopPolling();
          this.groupStatus.update((status) =>
            status ? { ...status, status: 'Submitted' } : status
          );
          this.submitDialogOpen.set(false);
          this.toastService.success('Pre-order sent to the restaurant');
          this.isSubmitting.set(false);
        },
        error: (error: unknown) => {
          const message =
            error instanceof HttpErrorResponse
              ? getServerErrorMessage(error)
              : '';
          this.toastService.error(message || 'Unable to submit the pre-order. Please try again.');
          this.isSubmitting.set(false);
        },
      });
  }

  private load(): void {
    this.loadSubscription?.unsubscribe();
    this.stopPolling();
    this.loadError.set(null);
    this.loading.set(true);
    this.invite.set(null);
    this.participants.set([]);
    this.guestSessionId.set(null);
    this.groupStatus.set(null);
    this.updatedAt.set(null);
    this.refreshFailed.set(false);

    const currentReservationId = this.reservationId();
    if (!currentReservationId) {
      this.loadError.set('Group reservation not found');
      this.loading.set(false);
      return;
    }

    this.loadSubscription = this.groupOrdersService
      .getGroupInvite$(currentReservationId)
      .pipe(
        switchMap((groupInvite) =>
          forkJoin({
            invite: of(groupInvite),
            sessions: this.groupOrdersService.getSessions$(groupInvite.GroupReservationId),
            ownerSession: this.groupOrdersService.getOwnerSessionId$(currentReservationId),
            status: this.groupOrdersService.getStatus$(groupInvite.GroupReservationId),
          })
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: ({ invite: groupInvite, sessions, ownerSession, status }) => {
          this.invite.set(groupInvite);
          this.participants.set(sessions.participants);
          this.guestSessionId.set(ownerSession);
          this.groupStatus.set(status);
          this.loading.set(false);
          this.updatedAt.set(new Date());
          if (status.status === 'Open') {
            this.startPolling();
          }
        },
        error: (error: unknown) => {
          this.loadError.set(
            error instanceof HttpErrorResponse && error.status === 404
              ? 'Group reservation not found'
              : 'Unable to load group reservation. Please try again.'
          );
          this.loading.set(false);
        },
      });
  }

  private startPolling(): void {
    this.stopPolling();
    this.pollSubscription = merge(
      interval(GROUP_ORDER_REFRESH_INTERVAL_MS),
      this.manualRefreshRequests
    )
      .pipe(
        exhaustMap(() =>
          this.refreshData().pipe(
            catchError(() => {
              this.refreshFailed.set(true);
              return EMPTY;
            })
          )
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(({ sessions, status }) => {
        if (sessions) {
          this.participants.set(sessions.participants);
        }
        if (status) {
          this.groupStatus.set(status);
        }

        const refreshSucceeded = sessions !== null && status !== null;
        if (refreshSucceeded) {
          this.updatedAt.set(new Date());
        }
        this.refreshFailed.set(!refreshSucceeded);

        if (status && status.status !== 'Open') {
          this.stopPolling();
        }
      });
  }

  private refreshData(): Observable<{
    sessions: GroupSessionsResponse | null;
    status: GroupStatusResponse | null;
  }> {
    const groupReservationId = this.invite()?.GroupReservationId;
    if (!groupReservationId) {
      return EMPTY;
    }

    return forkJoin({
      sessions: this.groupOrdersService
        .getSessions$(groupReservationId, { silent: true })
        .pipe(catchError(() => of(null))),
      status: this.groupOrdersService
        .getStatus$(groupReservationId, { silent: true })
        .pipe(catchError(() => of(null))),
    });
  }

  private stopPolling(): void {
    this.pollSubscription?.unsubscribe();
    this.pollSubscription = null;
  }
}
