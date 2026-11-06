// Naija Life Unity — unit tests for the `photon-auth` Edge Function.
// Run: deno test --allow-net --allow-env photon-auth.test.ts
// (from supabase/functions/ — no live project, no network: the Supabase
// client is fully mocked through the handler factory.)

import { assertEquals } from "jsr:@std/assert@1";
import { createHandler } from "./index.ts";
import type { AuthClient, VerifiedPlayer } from "../_shared/auth.ts";

const VALID_JWT = "valid.jwt.token";
const NO_PROFILE_JWT = "valid.jwt.no-profile";
const BAD_JWT = "invalid.jwt.token";

function mockClient(): AuthClient {
  const profiles = new Map<string, VerifiedPlayer>([
    ["11111111-1111-1111-1111-111111111111", {
      userId: "11111111-1111-1111-1111-111111111111",
      username: "TestName",
      avatarRef: "bust:v3:skin4:hair-dreads",
    }],
  ]);
  return {
    async getUser(jwt: string) {
      if (jwt === VALID_JWT) {
        return { userId: "11111111-1111-1111-1111-111111111111" };
      }
      if (jwt === NO_PROFILE_JWT) {
        return { userId: "99999999-9999-9999-9999-999999999999" };
      }
      return { userId: null };
    },
    async getProfile(userId: string, _jwt: string) {
      return profiles.get(userId) ?? null;
    },
  };
}

const handler = createHandler(mockClient());

async function post(body: Record<string, unknown>) {
  const req = new Request("http://localhost/functions/v1/photon-auth", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  const res = await handler(req);
  return { status: res.status, body: await res.json() as Record<string, unknown> };
}

Deno.test("valid JWT + profile -> 200 with UserId + Nickname", async () => {
  const { status, body } = await post({ token: VALID_JWT });
  assertEquals(status, 200);
  assertEquals(body.UserId, "11111111-1111-1111-1111-111111111111");
  assertEquals(body.Nickname, "TestName");
});

Deno.test("valid JWT, no profile -> 403 no_profile", async () => {
  const { status, body } = await post({ token: NO_PROFILE_JWT });
  assertEquals(status, 403);
  assertEquals(body.error, "no_profile");
});

Deno.test("invalid JWT -> 401 unauthorized", async () => {
  const { status, body } = await post({ token: BAD_JWT });
  assertEquals(status, 401);
  assertEquals(body.error, "unauthorized");
});

Deno.test("missing token -> 401 unauthorized", async () => {
  const { status, body } = await post({});
  assertEquals(status, 401);
  assertEquals(body.error, "unauthorized");
});

Deno.test("Authorization header bearer token works", async () => {
  const req = new Request("http://localhost/functions/v1/photon-auth", {
    method: "POST",
    headers: { Authorization: `Bearer ${VALID_JWT}` },
  });
  const res = await handler(req);
  assertEquals(res.status, 200);
  const body = await res.json() as Record<string, unknown>;
  assertEquals(body.Nickname, "TestName");
});

Deno.test("query-string token works", async () => {
  const req = new Request(
    `http://localhost/functions/v1/photon-auth?token=${VALID_JWT}`,
    { method: "POST" },
  );
  const res = await handler(req);
  assertEquals(res.status, 200);
});

Deno.test("non-POST/GET method -> 405", async () => {
  const req = new Request("http://localhost/functions/v1/photon-auth", {
    method: "PUT",
  });
  const res = await handler(req);
  assertEquals(res.status, 405);
});
