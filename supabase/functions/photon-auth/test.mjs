// Local check for photon-auth with Supabase mocked. Run: node --experimental-strip-types supabase/functions/photon-auth/test.mjs
const env={SUPABASE_URL:"https://x.supabase.co",SUPABASE_ANON_KEY:"anon",PHOTON_AUTH_KEY:"k123"};
globalThis.Deno={env:{get:k=>env[k]},serve:()=>{}};
let auth=200;
globalThis.fetch=async(u,o)=>{ if(o.headers.Authorization!=="Bearer a.b.c") return new Response("{}",{status:401}); return new Response(JSON.stringify({id:"11111111-2222-3333-4444-555555555555",email:"x@y"}),{status:auth});};
const {handler}=await import("./index.ts");
const call=async(q,body,m="POST")=>(await (await handler(new Request("https://f/photon-auth"+q,{method:m,body:m==="POST"?JSON.stringify(body):undefined}))).json());
const cases=[
 ["no key", await call("",{token:"a.b.c"}), r=>r.ResultCode===2],
 ["wrong key", await call("?k=nope",{token:"a.b.c"}), r=>r.ResultCode===2],
 ["no token", await call("?k=k123",{}), r=>r.ResultCode===3],
 ["junk token", await call("?k=k123",{token:"<script>"}), r=>r.ResultCode===3],
 ["bad token", await call("?k=k123",{token:"x.y.z"}), r=>r.ResultCode===2],
 ["good", await call("?k=k123",{token:"a.b.c"}), r=>r.ResultCode===1&&r.UserId==="11111111-2222-3333-4444-555555555555"&&!("email" in r)],
];
let f=0; for(const [n,r,ok] of cases){ const p=ok(r); if(!p)f++; console.log(p?"ok  ":"FAIL",n,JSON.stringify(r)); }
process.exit(f?1:0);
