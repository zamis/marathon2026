import { HttpClient, HttpContext } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable, switchMap } from 'rxjs';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { USE_OWNER_AUTH_FOR_GUEST_ENDPOINT } from '@app/auth/use-owner-auth.context';
import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { OwnerSessionResponse } from '@app/models/group-orders/owner-session.model';
import {
  SubmitGroupPreOrderRequest,
  SubmittedDish,
} from '@app/models/group-orders/submit-group-pre-order.model';
import { GuestInviteService } from '@app/services/reservation/guest-invite.service';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';

interface GroupRequestOptions {
  silent?: boolean;
}

@Injectable({
  providedIn: 'root',
})
export class GroupOrdersService {
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly http = inject(HttpClient);
  private readonly guestInviteService = inject(GuestInviteService);

  getGroupInvite$(reservationId: string): Observable<GuestInvite> {
    return this.guestInviteService
      .getInviteToken$(reservationId)
      .pipe(switchMap(({ token }) => this.guestInviteService.getInvite$(token)));
  }

  getSessions$(
    groupReservationId: string,
    options: GroupRequestOptions = {}
  ): Observable<GroupSessionsResponse> {
    let context = new HttpContext().set(USE_OWNER_AUTH_FOR_GUEST_ENDPOINT, true);
    if (options.silent) {
      context = context.set(SKIP_ERROR_TOAST, true);
    }

    return this.http.get<GroupSessionsResponse>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}/sessions`,
      { context }
    );
  }

  removeSession$(
    groupReservationId: string,
    guestSessionId: string
  ): Observable<GroupSessionsResponse> {
    return this.http.delete<GroupSessionsResponse>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}/sessions/${encodeURIComponent(guestSessionId)}`,
      {
        context: new HttpContext()
          .set(USE_OWNER_AUTH_FOR_GUEST_ENDPOINT, true)
          .set(SKIP_ERROR_TOAST, true),
      }
    );
  }

  getOwnerSessionId$(reservationId: string): Observable<string> {
    return this.http
      .get<OwnerSessionResponse>(
        `${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}/guest-session-id`
      )
      .pipe(map((response) => response.guestSessionId));
  }

  getStatus$(
    groupReservationId: string,
    options: GroupRequestOptions = {}
  ): Observable<GroupStatusResponse> {
    const context = options.silent
      ? new HttpContext().set(SKIP_ERROR_TOAST, true)
      : new HttpContext();
    return this.http.get<GroupStatusResponse>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}/status`,
      { context }
    );
  }

  submit$(groupReservationId: string, reservationId: string): Observable<SubmittedDish[]> {
    const body: SubmitGroupPreOrderRequest = { reservationId };
    return this.http.post<SubmittedDish[]>(
      `${this.baseUrl}/group-reservations/${encodeURIComponent(groupReservationId)}/submit`,
      body,
      { context: new HttpContext().set(SKIP_ERROR_TOAST, true) }
    );
  }
}
