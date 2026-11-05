// tools/camsim.js: maths check for the M1 camera follow-yaw rule (docs/reviews/m1-review.md R1).
// Run: node tools/camsim.js. Not Unity code; a quick model of PlayerController turning + PlayerCamera.UpdateFollowYaw.
// Sim of PlayerController turn + PlayerCamera follow-yaw rule, holding one key.
const da=(a,b)=>{let d=((b-a)%360+540)%360-180;return d;};
function run(key, mode, secs=6){
  let camYaw=0, face=0, dt=1/60, speed=0;
  const inp={W:[0,1],D:[1,0],S:[0,-1],A:[-1,0]}[key];
  for(let t=0;t<secs;t+=dt){
    const wish=camYaw+Math.atan2(inp[0],inp[1])*180/Math.PI;  // camera-relative
    let d=da(face,wish); face+=Math.sign(d)*Math.min(Math.abs(d),720*dt);  // RotateTowards
    speed=Math.min(4.2,speed+12*dt);
    const vel=wish; // velocity direction = wish direction
    if(speed>=0.6){
      const ref = mode==='old'? face : camYaw;   // old: velocity vs facing; fix: velocity vs camera
      if(Math.abs(da(vel,ref))<=35) camYaw+=da(camYaw,face)*(1-Math.exp(-1.6*dt));
    }
  }
  return {camYaw:Math.round(camYaw), face:Math.round(face)};
}
for(const k of ['W','D','S','A']) console.log(k,'old',JSON.stringify(run(k,'old')),'fixed',JSON.stringify(run(k,'fixed')));
