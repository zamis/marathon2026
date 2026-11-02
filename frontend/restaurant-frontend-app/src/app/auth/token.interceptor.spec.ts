import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { USE_OWNER_AUTH_FOR_GUEST_ENDPOINT } from './use-owner-auth.context';
import { ToastService } from '../services/toast/toast.service';
import { AuthenticationService } from './services/authentication.service';
import { SKIP_ERROR_TOAST } from './skip-error-toast.context';
import { tokenInterceptor } from './token.interceptor';

const URL = 'https://test.api/reservations';

describe('tokenInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let toastService: jasmine.SpyObj<ToastService>;
  let authService: jasmine.SpyObj<AuthenticationService>;

  beforeEach(() => {
    toastService = jasmine.createSpyObj<ToastService>('ToastService', ['error']);
    authService = jasmine.createSpyObj<AuthenticationService>('AuthenticationService', [
      'getTokens',
    ]);
    authService.getTokens.and.returnValue(null);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([tokenInterceptor])),
        provideHttpClientTesting(),
        { provide: ToastService, useValue: toastService },
        { provide: AuthenticationService, useValue: authService },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('shows the server message in a toast for a 400 response', () => {
    http.post(URL, {}).subscribe({ error: () => undefined });

    httpMock
      .expectOne(URL)
      .flush({ message: 'Overlaps' }, { status: 400, statusText: 'Bad Request' });

    expect(toastService.error).toHaveBeenCalledWith('Overlaps', 'Error');
  });

  it('does not show a toast but still rethrows when SKIP_ERROR_TOAST is set', () => {
    let receivedStatus: number | undefined;
    http
      .post(URL, {}, { context: new HttpContext().set(SKIP_ERROR_TOAST, true) })
      .subscribe({ error: (error) => (receivedStatus = error.status) });

    httpMock
      .expectOne(URL)
      .flush({ message: 'Overlaps' }, { status: 400, statusText: 'Bad Request' });

    expect(toastService.error).not.toHaveBeenCalled();
    expect(receivedStatus).toBe(400);
  });

  it('attaches the owner ID token to a guest endpoint when explicitly opted in', () => {
    authService.getTokens.and.returnValue({ idToken: 'owner-id-token', accessToken: 'access-token' });
    const context = new HttpContext().set(USE_OWNER_AUTH_FOR_GUEST_ENDPOINT, true);

    http.get(`${URL}/guest/groups/grp-1/sessions`, { context }).subscribe();
    const request = httpMock.expectOne(`${URL}/guest/groups/grp-1/sessions`);

    expect(request.request.context.get(USE_OWNER_AUTH_FOR_GUEST_ENDPOINT)).toBeTrue();
    expect(request.request.headers.get('Authorization')).toBe('Bearer owner-id-token');
    expect(authService.getTokens).toHaveBeenCalledTimes(1);
    request.flush({ participants: [], total: 0 });
  });

  it('keeps unopted guest requests on the guest-session bypass path', () => {
    authService.getTokens.and.returnValue({ idToken: 'owner-id-token', accessToken: 'access-token' });

    http.get(`${URL}/guest/sessions/session-1/menu`).subscribe();
    const request = httpMock.expectOne(`${URL}/guest/sessions/session-1/menu`);

    expect(request.request.context.get(USE_OWNER_AUTH_FOR_GUEST_ENDPOINT)).toBeFalse();
    expect(request.request.headers.has('Authorization')).toBeFalse();
    expect(authService.getTokens).not.toHaveBeenCalled();
    request.flush({ dishes: {} });
  });
});
