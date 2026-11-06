-- ============================================================================
-- Naija Life Unity — Migration 0004: street card by user id (M5.1f)
--
-- WHY: the live browser nl_street_card() takes a USERNAME, but the
-- multiplayer layer only knows user IDS (Fusion player ids are Supabase
-- auth user ids; identity comes from the street-card RPC, never from the
-- movement payload — migration plan §4). This RPC resolves a card by id.
--
-- RETURN SHAPE (mirrors browser nl_street_card, minus the society
-- machinery — no crews/bans/audit in the Unity v1):
--   {ok, username, avatar_ref, member_since, crew: null, office: null,
--    rep_tier: null}
-- Errors: {ok:false, error:'denied'|'no_such_player'}.
--
-- PRIVACY: usernames + avatar refs are PUBLIC by design (name tags above
-- players' heads). Nothing else is returned — no email, no wallet, no
-- location. Caller must be authenticated (a signed-in player looking up
-- another player they can see in-world).
--
-- OWNER-GATED: this file is WRITTEN now (M5.1 needs it) but is applied
-- only with Benjamin's explicit OK, like every live migration. Claude
-- reviews M5.1f before application (TASKS.md M5.1).
--
-- STATUS: written 2026-11-05 (Unity backend builder). NOT APPLIED.
-- Tested on local Postgres 16 with stub auth.uid()/auth.role().
-- ============================================================================

create or replace function public.nl_street_card_by_id(p_user_id uuid)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
  v_me   uuid := auth.uid();
  v_prof record;
begin
  if v_me is null then
    return jsonb_build_object('ok', false, 'error', 'denied');
  end if;
  if p_user_id is null then
    return jsonb_build_object('ok', false, 'error', 'no_such_player');
  end if;

  select username, avatar_ref, created_at into v_prof
    from public.profiles where id = p_user_id;
  if not found then
    return jsonb_build_object('ok', false, 'error', 'no_such_player');
  end if;

  return jsonb_build_object(
    'ok', true,
    'username', v_prof.username,
    'avatar_ref', v_prof.avatar_ref,
    'member_since', v_prof.created_at,
    'crew', null,        -- crews fill this in (society workstream)
    'office', null,      -- S-4 fills this in
    'rep_tier', null     -- rep engine fills this in
  );
end;
$$;

comment on function public.nl_street_card_by_id(uuid) is
  'Unity 0004 (M5.1f): public street card for a player, resolved by auth '
  'user id (the multiplayer layer only knows ids). Same return shape as '
  'browser nl_street_card. Caller must be authenticated. Owner-gated '
  'for application.';

-- Player RPC: authenticated may CALL; nobody else.
revoke all on function public.nl_street_card_by_id(uuid) from public, anon;
grant execute on function public.nl_street_card_by_id(uuid) to authenticated;

-- ============================================================================
-- CHECK QUERIES (run after applying, paste output in the commit).
--
-- -- 1. RPC exists + grant
-- select p.proname from pg_proc p join pg_namespace n on n.oid = p.pronamespace
--   where n.nspname = 'public' and p.proname = 'nl_street_card_by_id';
-- select has_function_privilege('anon', 'public.nl_street_card_by_id(uuid)', 'execute') as anon,
--        has_function_privilege('authenticated', 'public.nl_street_card_by_id(uuid)', 'execute') as auth;
-- -- expect: f / t
--
-- -- 2. behaviour (as a signed-in user; <id> = a profile that exists):
-- -- select public.nl_street_card_by_id('<id>');
-- --   -> {ok:true, username:'...', avatar_ref:..., member_since:...}
-- -- select public.nl_street_card_by_id('00000000-0000-0000-0000-000000000000');
-- --   -> {ok:false, error:'no_such_player'}
-- -- (no session): select public.nl_street_card_by_id('<id>');
-- --   -> {ok:false, error:'denied'}
-- ============================================================================
