// Naija Life Unity — shared auth helper for Edge Functions.
//
// Verifies a Supabase JWT (anonymous or email user) and resolves the
// caller's profile. Uses explicit `npm:` specifiers — the server-side
// bundler does NOT apply deno.json import maps to brand-new functions,
// so bare `@supabase/supabase-js` imports fail (see AGENTS.md).
//
// SECURITY: no service_role key is used anywhere in this backend. The
// helper verifies the token against the Auth server with the anon key
// and reads the profile with the CALLER's own JWT, so RLS still applies
// (the function can only ever see the caller's own profile row).

import { createClient, type SupabaseClient } from "npm:@supabase/supabase-js@2";

export interface VerifiedPlayer {
  userId: string;
  username: string;
  avatarRef: string | null;
}

export interface AuthClient {
  getUser: (jwt: string) => Promise<{ userId: string | null }>;
  getProfile: (userId: string, jwt: string) => Promise<VerifiedPlayer | null>;
}

/** Build the real auth client from the Edge Function environment. */
export function createAuthClient(): AuthClient {
  const url = Deno.env.get("SUPABASE_URL");
  const anonKey = Deno.env.get("SUPABASE_ANON_KEY");
  if (!url || !anonKey) {
    throw new Error("SUPABASE_URL / SUPABASE_ANON_KEY are not set");
  }
  const admin: SupabaseClient = createClient(url, anonKey);
  return {
    async getUser(jwt: string) {
      const { data, error } = await admin.auth.getUser(jwt);
      if (error || !data?.user) return { userId: null };
      return { userId: data.user.id };
    },
    async getProfile(userId: string, jwt: string) {
      // Read with the CALLER's JWT so RLS applies: the function can only
      // ever see the caller's own profile row.
      const authed: SupabaseClient = createClient(url, anonKey, {
        global: { headers: { Authorization: `Bearer ${jwt}` } },
      });
      const { data, error } = await authed
        .from("profiles")
        .select("id, username, avatar_ref")
        .eq("id", userId)
        .single();
      if (error || !data) return null;
      return {
        userId: data.id as string,
        username: data.username as string,
        avatarRef: (data.avatar_ref as string | null) ?? null,
      };
    },
  };
}

/**
 * Verify a JWT and resolve the caller's profile.
 * Returns the player, or null when the token is invalid / the player has
 * no profile yet (onboarding not completed).
 */
export async function verifyPlayer(
  jwt: string | null,
  client: AuthClient,
): Promise<VerifiedPlayer | null> {
  if (!jwt) return null;
  const { userId } = await client.getUser(jwt);
  if (!userId) return null;
  return await client.getProfile(userId, jwt);
}

/**
 * Pull a token out of a Photon custom-auth webhook request. Photon
 * forwards the client's custom auth parameters; the Unity client sends
 * `token=<supabase jwt>`. Accept JSON body, form body, query string,
 * and Authorization header, in that order.
 */
export async function extractToken(req: Request): Promise<string | null> {
  const fromHeader = req.headers.get("authorization");
  if (fromHeader?.toLowerCase().startsWith("bearer ")) {
    return fromHeader.slice(7).trim() || null;
  }
  const url = new URL(req.url);
  const fromQuery = url.searchParams.get("token") ??
    url.searchParams.get("authToken");
  if (fromQuery) return fromQuery;

  const contentType = req.headers.get("content-type") ?? "";
  try {
    if (contentType.includes("application/json")) {
      const body = await req.json() as Record<string, unknown>;
      const t = body.token ?? body.authToken ?? body.jwt;
      return typeof t === "string" && t ? t : null;
    }
    if (contentType.includes("application/x-www-form-urlencoded")) {
      const form = await req.formData();
      const t = form.get("token") ?? form.get("authToken");
      return typeof t === "string" && t ? t : null;
    }
  } catch {
    return null;
  }
  return null;
}
