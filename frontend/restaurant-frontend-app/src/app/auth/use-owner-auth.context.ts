import { HttpContextToken } from '@angular/common/http';

/**
 * Marks a guest-route request that is authorized with the signed-in owner's ID token.
 * Guest-session requests remain untouched unless they explicitly opt in.
 */
export const USE_OWNER_AUTH_FOR_GUEST_ENDPOINT = new HttpContextToken<boolean>(() => false);
