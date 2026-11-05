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

// ---- M3 driving cases (docs/specs/vehicles-m3.md section 5) ----
// The target is a vehicle: steering is vehicle-relative, so heading changes come from the car, not the camera.
// walk = the M1 walking rule (velocity vs camera within 35 deg, recenter 1.6). drive = UpdateDrivingYaw
// (forward speed >= 1.5, slide <= 50 deg, recenter 2.4).
function drive(caseName, rule){
  let cam=0, nose=0, velDir=0, fwd=0, dt=1/60, maxLag=0;
  for(let t=0;t<8;t+=dt){
    let rate=0; fwd=12;                               // m/s along the nose
    if(caseName==='turn90'){ if(t>1&&t<2.5) rate=60; velDir=nose; }          // sharp 90 deg corner at 60 deg/s
    if(caseName==='uturn'){ if(t>1&&t<4) rate=60; velDir=nose; }             // 180 deg U-turn
    if(caseName==='reverse'){ fwd=-4; if(t>1&&t<3) rate=30; velDir=nose+180; } // reversing while steering
    if(caseName==='spin'){ if(t>1&&t<2.5) rate=240; velDir=0; fwd=12*Math.cos((nose-velDir)*Math.PI/180); } // spin-out, travel stays north
    nose+=rate*dt; if(caseName!=='spin'&&caseName!=='reverse') velDir=nose;
    const speed=Math.abs(caseName==='reverse'?4:12);
    if(rule==='walk'){
      if(speed>=0.6 && Math.abs(da(velDir,cam))<=35) cam+=da(cam,nose)*(1-Math.exp(-1.6*dt));
    } else {
      if(fwd>=1.5 && Math.abs(da(velDir,nose))<=50) cam+=da(cam,nose)*(1-Math.exp(-2.4*dt));
    }
    maxLag=Math.max(maxLag,Math.abs(da(cam,nose)));
  }
  return {endLag:Math.round(Math.abs(da(cam,nose))), maxLag:Math.round(maxLag)};
}
for(const c of ['turn90','uturn','reverse','spin'])
  console.log('drive',c,'walkRule',JSON.stringify(drive(c,'walk')),'driveRule',JSON.stringify(drive(c,'drive')));
