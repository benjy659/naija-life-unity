// Naija Life Unity — Edge Function `photon-auth` (TASKS.md M5.1b)
//
// Photon Fusion 2 custom authentication webhook. Photon calls this when a
// player joins a room; the Unity client sends its Supabase JWT as the
// custom auth parameter `token`. This function verifies the JWT against
// the Supabase Auth server and requires a completed profile (onboarding
// done) before the player may join.
//
// Photon contract: return HTTP 200 with a JSON object. The returned
// `UserId` becomes the player's Photon UserId; `Nickname` is the
// in-world display name. Any non-200 (or a 200 without UserId) rejects
// the join.
//
// Responses:
//   200 { UserId, Nickname }            — verified player with a profile
//   401 { error: 'unauthorized' }       — missing / invalid / expired JWT
//   403 { error: 'no_profile' }         — valid JWT, onboarding not done
//   500 { error: 'misconfigured' }      — server env missing (never a client fault)
//
// SECURITY:
// - Photon "anonymous" auth is OFF: every player must present a Supabase
//   JWT (anonymous sign-in or email). There is no guest path.
// - No service_role key is used. The JWT is verified with the anon key
//   and the profile is read with the caller's own JWT, so RLS applies.
// - Fusion never carries money, NIN, email or date of birth: only the
//   user id + public username leave this function.
// - This function does NOT mint anything and does NOT touch the ledger.
//
// Deploy (owner-gated with the migrations — see supabase/README.md):
//   supabase functions deploy photon-auth --project-ref jdumcxvqjtgnbeuvlufq
// Photon dashboard (manual, once): Fusion app -> Custom Authentication ->
//   webhook URL = https://jdumcxvqjtgnbeuvlufq.supabase.co/functions/v1/photon-auth
//   (POST). No secret needed: the Supabase JWT is the credential.

import { createAuthClient, extractToken, verifyPlayer, type AuthClient } from "../_shared/auth.ts";

const json = (status: number, body: Record<string, unknown>) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });

/** Handler factory: inject a mock AuthClient in tests, use the real one in prod. */
export function createHandler(client: AuthClient) {
  return async (req: Request): Promise<Response> => {
    if (req.method !== "POST" && req.method !== "GET") {
      return json(405, { error: "method_not_allowed" });
    }
    let token: string | null;
    try {
      token = await extractToken(req);
    } catch {
      return json(401, { error: "unauthorized" });
    }
    if (!token) return json(401, { error: "unauthorized" });

    let player;
    try {
      player = await verifyPlayer(token, client);
    } catch {
      return json(401, { error: "unauthorized" });
    }
    if (!player) {
      // Distinguish "bad token" from "good token, no profile yet": the
      // client re-checks getUser — if the token is valid the player
      // simply hasn't finished onboarding.
      const { userId } = await client.getUser(token).catch(() => ({ userId: null }));
      if (!userId) return json(401, { error: "unauthorized" });
      return json(403, { error: "no_profile" });
    }

    return json(200, { UserId: player.userId, Nickname: player.username });
  };
}

// Production entrypoint: only build the real client when the function runs.
if (import.meta.main) {
  let handler: (req: Request) => Promise<Response>;
  try {
    handler = createHandler(createAuthClient());
  } catch {
    // Env missing (SUPABASE_URL / SUPABASE_ANON_KEY): fail closed with 500.
    handler = () => Promise.resolve(json(500, { error: "misconfigured" }));
  }
  Deno.serve(handler);
}
