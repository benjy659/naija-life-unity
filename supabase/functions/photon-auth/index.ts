// Naija Streets (Unity) — M5.1b: Photon custom authentication (DRAFT, 2026-11-06).
// Spec: docs/specs/netcode-m5.md §5.1.
//
// Photon calls this URL when a client joins. The client sends its Supabase
// access token in the auth POST body ({"token": "<jwt>"}). We check it with
// Supabase Auth and tell Photon who the player is. Photon then stamps that
// UserId on the player, so nobody can pose as someone else on the network.
//
// STATUS: DRAFT by Claude, not deployed. Ted's team deploys:
//   supabase functions deploy photon-auth --no-verify-jwt
//   (--no-verify-jwt because Photon, not a signed-in player, makes the call;
//    the token is checked below instead.)
// Secrets (set in Supabase secrets, never in git):
//   PHOTON_AUTH_KEY  a long random string. In the Photon dashboard, set the
//                    custom auth URL to .../functions/v1/photon-auth?k=<same string>
//                    so only Photon can use this function.
// SUPABASE_URL and SUPABASE_ANON_KEY are provided by Supabase automatically.
// Photon dashboard: Authentication > Custom Server, and turn OFF "Allow
// anonymous clients to connect".
//
// Reply format (Photon custom auth): ResultCode 1 = ok with UserId,
// 2 = refused, 3 = bad parameters. Always HTTP 200 so Photon reads the body.
// Never logs the token. Never returns email, NIN, date of birth or money.

const SUPABASE_URL = Deno.env.get("SUPABASE_URL") ?? "";
const ANON_KEY = Deno.env.get("SUPABASE_ANON_KEY") ?? "";
const PHOTON_AUTH_KEY = Deno.env.get("PHOTON_AUTH_KEY") ?? "";

type PhotonReply = { ResultCode: number; UserId?: string; Message?: string };

function reply(body: PhotonReply): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { "Content-Type": "application/json", "Cache-Control": "no-store" },
  });
}

/** Constant-time string compare, so the key cannot be guessed by timing. */
function sameKey(a: string, b: string): boolean {
  if (!a || !b || a.length !== b.length) return false;
  let diff = 0;
  for (let i = 0; i < a.length; i++) diff |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return diff === 0;
}

async function readToken(req: Request): Promise<string | null> {
  if (req.method !== "POST") return null;
  try {
    const body = await req.json();
    const t = body && typeof body.token === "string" ? body.token : null;
    // A Supabase JWT is three base64url parts; reject anything else early.
    if (!t || t.length > 4096 || !/^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$/.test(t)) return null;
    return t;
  } catch {
    return null;
  }
}

export async function handler(req: Request): Promise<Response> {
  const url = new URL(req.url);
  if (!PHOTON_AUTH_KEY || !sameKey(url.searchParams.get("k") ?? "", PHOTON_AUTH_KEY)) {
    return reply({ ResultCode: 2, Message: "not allowed" });
  }

  const token = await readToken(req);
  if (!token) return reply({ ResultCode: 3, Message: "missing token" });

  // Ask Supabase Auth who owns this token (checks signature and expiry).
  let res: Response;
  try {
    res = await fetch(`${SUPABASE_URL}/auth/v1/user`, {
      headers: { apikey: ANON_KEY, Authorization: `Bearer ${token}` },
      signal: AbortSignal.timeout(5000),
    });
  } catch {
    return reply({ ResultCode: 2, Message: "auth unavailable" });
  }
  if (!res.ok) return reply({ ResultCode: 2, Message: "sign in again" });

  const user = await res.json().catch(() => null);
  const id = user && typeof user.id === "string" ? user.id : "";
  if (!/^[0-9a-f-]{36}$/i.test(id)) return reply({ ResultCode: 2, Message: "sign in again" });

  // TODO (after M5.1): refuse banned players here too, through a small
  // security-definer RPC (same rule as _nl_society_banned), once Ted adds one.
  return reply({ ResultCode: 1, UserId: id });
}

Deno.serve(handler);
