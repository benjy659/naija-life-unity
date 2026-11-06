-- ============================================================================
-- Naija Streets (Unity) — M5.1f: street card by user id (DRAFT, 2026-11-06)
-- Migration 0041: nl_street_card_by_id RPC. Spec: docs/specs/netcode-m5.md §3.5.
--
-- STATUS: DRAFT by Claude, NOT applied. Ted's team owns migrations: review,
-- adjust, and apply ONLY after Benjamin's explicit OK. Claude never applies.
--
-- WHY: the live nl_street_card(p_username, p_request_id) finds players by
-- username. In the Unity game the network only knows the server-stamped user
-- id (Photon custom auth -> auth.uid()), so name tags need a lookup by id.
--
-- SHAPE: creates ONE new function. No ALTER on any live object, no new
-- tables, no data changes. Same checks and same reply as nl_street_card
-- (banned caller, blocked pair, audit row, rate limit), same jsonb keys, so
-- clients parse one shape. Own limit: 600 calls an hour per caller (one
-- district room of 80, joins and leaves, 10-minute client cache).
-- Never returns NIN, email, date of birth, money or location.
-- ============================================================================

create or replace function public.nl_street_card_by_id(p_uid uuid, p_request_id uuid default null)
returns jsonb
language plpgsql
security definer
set search_path to 'public'
as $function$
declare
  v_me uuid := auth.uid();
  v_rid uuid := coalesce(p_request_id, gen_random_uuid());
  v_prof record;
  v_crew record;
begin
  if v_me is null or public._nl_society_banned() then
    perform public._nl_society_audit(v_me, 'street_card_id', jsonb_build_object('uid', p_uid), null, null, false, v_rid);
    return jsonb_build_object('ok', false, 'error', 'denied');
  end if;

  if p_uid is null then
    return jsonb_build_object('ok', false, 'error', 'no_such_player');
  end if;

  if public._nl_is_blocked(v_me, p_uid) then
    perform public._nl_society_audit(v_me, 'street_card_id', jsonb_build_object('uid', p_uid), null, null, false, v_rid);
    return jsonb_build_object('ok', false, 'error', 'blocked');
  end if;

  if not public._nl_society_limit(v_me, 'street_card_id', 600, interval '1 hour') then
    perform public._nl_society_audit(v_me, 'street_card_id', jsonb_build_object('uid', p_uid), null, null, false, v_rid);
    return jsonb_build_object('ok', false, 'error', 'rate_limited');
  end if;

  select username, avatar_ref, created_at into v_prof
    from public.profiles where id = p_uid;
  if v_prof.username is null then
    -- No profile yet (new anonymous account): no card, never a made-up name.
    return jsonb_build_object('ok', false, 'error', 'no_such_player');
  end if;

  select c.name, c.type, c.emblem, c.city, m.role into v_crew
    from public.crew_members m join public.crews c on c.id = m.crew_id
   where m.user_id = p_uid and c.disbanded_at is null;

  perform public._nl_society_audit(v_me, 'street_card_id',
    jsonb_build_object('uid', p_uid), null, null, true, v_rid);

  return jsonb_build_object(
    'ok', true,
    'username', v_prof.username,
    'avatar_ref', v_prof.avatar_ref,
    'member_since', v_prof.created_at,
    'crew', case when v_crew.name is null then null
                 else jsonb_build_object('name', v_crew.name, 'type', v_crew.type,
                                         'emblem', v_crew.emblem, 'city', v_crew.city,
                                         'role', v_crew.role) end,
    'office', null,
    'rep_tier', null
  );
end $function$;

revoke all on function public.nl_street_card_by_id(uuid, uuid) from public, anon;
grant execute on function public.nl_street_card_by_id(uuid, uuid) to authenticated, service_role;

-- Verify after apply (Ted):
--   select public.nl_street_card_by_id('<a real profile uid>');           -- as an authenticated user: ok true
--   select public.nl_street_card_by_id(gen_random_uuid());               -- ok false, no_such_player
--   anon role: permission denied
