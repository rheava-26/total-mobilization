/* =====================================================================
   fleet-light.js — MODELS MODULE: small craft, submarines & aircraft
   (procedural models). Companion to game.html's MODELS registry —
   NOT loaded by game.html directly here; the orchestrator wires it up.
   Contract: makeFleet(H) -> {units, models}
     H = {THREE, MAT, box, cyl, buildHull, finishShip, turretGroup}
   Sea units: buildHull(d) + finishShip(g,d).
   Air units: hand-built THREE.Group + invisible pick box; set
   g.userData.rotor on anything with a spinning main rotor (helis).
   ===================================================================== */
export function makeFleet(H){
  const {THREE, MAT, box, cyl, buildHull, finishShip, turretGroup} = H;

  /* ============================ UNITS ============================ */
  const units = {
    ptboat:{ name:'PT-9 Torpedo Boat', cls:'PT BOAT', model:'ptboat', domain:'sea',
      len:24, beam:5, mass:50, hp:70,
      turrets:[
        {w:'lightMG', pos:[0,0.7,8], barrel:1.6},
        {w:'lightMG', pos:[0,0.7,-8], barrel:1.6, scale:0.9} ] },

    lcs:{ name:'LCS-Type Littoral Ship', cls:'LCS', model:'lcs', domain:'sea',
      len:38, beam:7, mass:2800, hp:340,
      turrets:[
        {w:'mainGun',   pos:[0,0.9,15], barrel:4.6},
        {w:'autocannon',pos:[0,2.6,-4], barrel:2.0},
        {w:'rocket',    pos:[0,1.4,-14], barrel:1.6} ] },

    minesweeper:{ name:'MSW-3 Minesweeper', cls:'MINESWEEPER', model:'minesweeper', domain:'sea',
      len:30, beam:6.5, mass:700, hp:180,
      turrets:[
        {w:'autocannon', pos:[0,0.8,10], barrel:2.0},
        {w:'lightMG',    pos:[0,0.8,-2], barrel:1.6, scale:0.9} ] },

    missilecorvette:{ name:'MCV-6 Missile Corvette', cls:'MISSILE CORVETTE', model:'missilecorvette', domain:'sea',
      len:32, beam:6, mass:850, hp:200,
      turrets:[
        {w:'rocket',     pos:[-1.2,0.7,-3], barrel:1.6},
        {w:'rocket',     pos:[ 1.2,0.7,-3], barrel:1.6},
        {w:'autocannon', pos:[0,0.8,11], barrel:2.0} ] },

    landingcraft:{ name:'LCU-2 Landing Craft', cls:'LANDING CRAFT', model:'landingcraft', domain:'sea',
      len:26, beam:7, mass:380, hp:160,
      turrets:[ {w:'lightMG', pos:[0,1.4,-9], barrel:1.6} ] },

    submarine:{ name:'SSK-1 Attack Submarine', cls:'SUBMARINE', model:'submarine', domain:'sea',
      len:50, beam:6, mass:2500, hp:280,
      turrets:[ {w:'lightMG', pos:[0,3.6,-4], barrel:1.6, scale:0.9} ] },

    seahawk:{ name:'SH-70 Seahawk', cls:'NAVAL HELI', model:'seahawk', domain:'air',
      len:15, beam:3, mass:50, hp:180,
      turrets:[
        {w:'lightMG', pos:[-1.6,-0.3,1], barrel:1.2},
        {w:'rocket',  pos:[1.6,-0.3,0], barrel:1.4} ] },

    cobra:{ name:'AH-1 Cobra Gunship', cls:'GUNSHIP', model:'cobra', domain:'air',
      len:14, beam:2.5, mass:45, hp:220,
      turrets:[
        {w:'rocket',     pos:[0,-0.3,3], barrel:1.4},
        {w:'autocannon', pos:[0,-0.6,4], barrel:1.6, scale:0.5} ] },

    seaplane:{ name:'PBY Recon Seaplane', cls:'SEAPLANE', model:'seaplane', domain:'air',
      len:12, beam:3, mass:30, hp:120,
      turrets:[ {w:'lightMG', pos:[0,0.2,4], barrel:1.2} ] },

    patrolbomber:{ name:'PB-4 Patrol Bomber', cls:'PATROL BOMBER', model:'patrolbomber', domain:'air',
      len:20, beam:4, mass:90, hp:260,
      turrets:[
        {w:'rocket',  pos:[0,0.2,7], barrel:1.6},
        {w:'lightMG', pos:[-2.2,0.6,-1], barrel:1.2, scale:0.9},
        {w:'lightMG', pos:[2.2,0.6,-1], barrel:1.2, scale:0.9} ] },
  };

  /* ============================ MODELS ============================ */
  const models = {

    // ---- 1. PT torpedo boat: low, fast, slim hull, twin MGs, torpedo tubes as deck decor ----
    ptboat(d){ const g=buildHull(d), B=d.beam, L=d.len;
      g.add(box(B*0.5,0.8,L*0.2, MAT.sup, 0,0.65,-L*0.1));      // small low cabin
      g.add(box(B*0.34,0.5,1.0, MAT.glass, 0,1.15,L*0.02));     // windscreen
      g.add(box(0.1,2.2,0.1,MAT.steel,0,1.6,-L*0.14));          // whip antenna mast
      [-1,1].forEach(s=>{ const tube=cyl(0.3,0.3,4.4,MAT.dark,10);
        tube.rotation.z=Math.PI/2; tube.position.set(s*B*0.34,0.55,-L*0.02); g.add(tube); });
      return finishShip(g,d); },

    // ---- 2. LCS-style littoral ship: angular sloped superstructure ----
    lcs(d){ const g=buildHull(d), B=d.beam, L=d.len;
      const bridge=box(B*0.62,1.8,L*0.2, MAT.sup, 0,1.1,-L*0.04); g.add(bridge);
      const slope=box(B*0.6,1.3,0.6, MAT.supHi, 0,2.1,L*0.05); slope.rotation.x=-0.4; g.add(slope);
      const slopeAft=box(B*0.5,1.0,0.6, MAT.supHi, 0,1.9,-L*0.16); slopeAft.rotation.x=0.4; g.add(slopeAft);
      g.add(box(B*0.4,0.6,1.6, MAT.glass, 0,2.6,L*0.06));
      const mast=box(0.16,4.6,0.9,MAT.steel,0,3.6,-L*0.06); mast.rotation.x=-0.12; g.add(mast);
      g.add(box(B*0.36,0.7,L*0.18, MAT.dark, 0,0.5,-L*0.32));   // helipad-ish aft deck box
      return finishShip(g,d); },

    // ---- 3. Minesweeper: stubby hull, big aft crane arm ----
    minesweeper(d){ const g=buildHull(d), B=d.beam, L=d.len;
      g.add(box(B*0.58,1.3,L*0.22, MAT.sup, 0,0.85,-L*0.02));
      g.add(box(B*0.42,0.7,1.1, MAT.glass, 0,1.65,L*0.03));
      g.add(box(0.12,2.6,0.12,MAT.steel,0,2.2,-L*0.06));
      const craneBase=box(0.5,1.6,0.5,MAT.steel, 0,0.9,-L*0.38); g.add(craneBase);
      const arm=box(0.3,0.3,4.2,MAT.dark, 0,1.7,-L*0.38); arm.rotation.x=-0.55; g.add(arm);
      const arm2=box(0.24,0.24,2.6,MAT.dark, 0,2.9,-L*0.42); arm2.rotation.x=-0.95; g.add(arm2);
      return finishShip(g,d); },

    // ---- 4. Missile corvette: 4 angled missile tubes amidships ----
    missilecorvette(d){ const g=buildHull(d), B=d.beam, L=d.len;
      g.add(box(B*0.6,1.3,L*0.2, MAT.sup, 0,0.85,L*0.08));
      g.add(box(B*0.44,0.7,1.2, MAT.glass, 0,1.65,L*0.16));
      g.add(box(0.12,3.0,0.12,MAT.steel,0,2.3,L*0.06));
      const tubeXs=[-1.6,-0.55,0.55,1.6];
      tubeXs.forEach(x=>{ const t=cyl(0.28,0.34,3.4,MAT.dark,10);
        t.rotation.x=-0.5; t.position.set(x,1.2,-L*0.06); g.add(t); });
      return finishShip(g,d); },

    // ---- 5. Landing craft: flat open box well deck, bow ramp ----
    landingcraft(d){ const g=buildHull(d), B=d.beam, L=d.len;
      g.add(box(B*0.94,0.14,L*0.62, MAT.dark, 0,0.5,-L*0.06));   // well-deck floor
      [-1,1].forEach(s=> g.add(box(0.16,1.1,L*0.62, MAT.dark, s*B*0.47,1.0,-L*0.06)));  // well-deck sides
      const ramp=box(B*0.9,0.14,L*0.24, MAT.steel, 0,0.35,L*0.36); ramp.rotation.x=-0.5; g.add(ramp);
      g.add(box(B*0.5,1.6,L*0.14, MAT.sup, 0,1.2,-L*0.42));      // small aft wheelhouse
      g.add(box(0.1,2.0,0.1,MAT.steel,0,2.4,-L*0.44));
      return finishShip(g,d); },

    // ---- 6. Submarine: NOT buildHull — capsule/cylinder hull, riding low, conning-tower sail ----
    submarine(d){ const g=new THREE.Group(), B=d.beam, L=d.len;
      const hull=new THREE.Mesh(new THREE.CapsuleGeometry(B*0.42, L*0.72, 6, 14), MAT.hullLo);
      hull.rotation.x=Math.PI/2; hull.position.y=-B*0.28; g.add(hull);
      const deckStrip=box(B*0.35,0.14,L*0.9, MAT.dark, 0,-B*0.02,0); g.add(deckStrip);
      const sail=box(B*0.5,2.6,L*0.16, MAT.sup, 0,1.1,L*0.06); g.add(sail);
      g.add(box(B*0.3,0.5,0.5, MAT.glass, 0,2.4,L*0.1));
      const periscope=box(0.14,1.6,0.14,MAT.steel,0,3.4,L*0.02); g.add(periscope);
      [-1,1].forEach(s=> g.add(box(0.9,0.16,0.5, MAT.dark, s*0.7, -B*0.3, -L*0.42))); // stern planes
      const turrets=d.turrets.map(t=>{ const tg=turretGroup(t); g.add(tg.pivot); return tg; });
      const pick=new THREE.Mesh(new THREE.BoxGeometry(B*1.3, 3.4, L*1.02), new THREE.MeshBasicMaterial({visible:false}));
      pick.position.y=0.6; g.add(pick);
      return {group:g,turrets,pick}; },

    // ---- 7. Seahawk: naval heli, rotor via userData.rotor ----
    seahawk(d){ const g=new THREE.Group();
      const body=new THREE.Mesh(new THREE.CapsuleGeometry(1.3,3.4,5,10),MAT.heli); body.rotation.x=Math.PI/2; g.add(body);
      g.add(box(1.6,1.0,1.5,MAT.glass,0,0.3,2.6));
      g.add(box(0.65,0.65,4.8,MAT.heli,0,0.3,-3.9));
      g.add(box(2.2,0.12,0.5,MAT.dark,0,0.85,-6.0));
      g.add(box(0.5,1.3,0.12,MAT.heli,0,0.8,-6.2));
      const rotor=new THREE.Group(); rotor.position.y=1.6; rotor.add(box(0.36,0.08,12,MAT.dark)); rotor.add(box(12,0.08,0.36,MAT.dark));
      g.add(rotor); g.userData.rotor=rotor;
      g.add(box(0.14,1.3,0.14,MAT.steel,0,0.9,0));
      [-1,1].forEach(s=> g.add(box(0.14,0.14,3.4,MAT.dark, s*1.05,-1.2,0.1)));
      [-1,1].forEach(s=> g.add(box(0.1,0.8,0.1,MAT.steel, s*0.9,-0.75,0.1)));
      const turrets=d.turrets.map(t=>{ const tg=turretGroup(t); tg.pivot.scale.setScalar(0.4); g.add(tg.pivot); return tg; });
      const pick=new THREE.Mesh(new THREE.BoxGeometry(3.6,3.2,10), new THREE.MeshBasicMaterial({visible:false})); g.add(pick);
      return {group:g,turrets,pick}; },

    // ---- 8. Cobra: slim tandem attack gunship, stub wings w/ rocket pods, rotor ----
    cobra(d){ const g=new THREE.Group();
      const body=new THREE.Mesh(new THREE.CapsuleGeometry(0.85,4.6,5,10),MAT.heli); body.rotation.x=Math.PI/2; g.add(body);
      g.add(box(0.9,0.85,1.6,MAT.glass,0,0.15,3.0));            // tandem canopy
      g.add(box(0.55,0.55,4.4,MAT.heli,0,0.05,-3.6));           // slim tail boom
      g.add(box(1.8,0.1,0.42,MAT.dark,0,0.6,-5.6));
      g.add(box(0.4,1.1,0.1,MAT.heli,0,0.55,-5.8));
      const wingL=box(2.4,0.24,0.7,MAT.dark,-1.5,-0.1,0.2); g.add(wingL);
      const wingR=box(2.4,0.24,0.7,MAT.dark,1.5,-0.1,0.2); g.add(wingR);
      [-2.5,-0.7,0.7,2.5].forEach(x=>{ const pod=cyl(0.18,0.18,1.4,MAT.dark,8); pod.rotation.x=Math.PI/2; pod.position.set(x,-0.3,0.6); g.add(pod); });
      const rotor=new THREE.Group(); rotor.position.y=1.2; rotor.add(box(0.3,0.07,11,MAT.dark)); rotor.add(box(11,0.07,0.3,MAT.dark));
      g.add(rotor); g.userData.rotor=rotor;
      g.add(box(0.12,1.1,0.12,MAT.steel,0,0.65,0));
      const skid=(s)=>box(0.1,0.1,3.6,MAT.dark, s*0.75,-1.0,0.1);
      g.add(skid(-1)); g.add(skid(1));
      const turrets=d.turrets.map(t=>{ const tg=turretGroup(t); tg.pivot.scale.setScalar(0.4); g.add(tg.pivot); return tg; });
      const pick=new THREE.Mesh(new THREE.BoxGeometry(5.2,2.2,10.5), new THREE.MeshBasicMaterial({visible:false})); g.add(pick);
      return {group:g,turrets,pick}; },

    // ---- 9. Seaplane: fixed wings, underwing floats, prop disc. No rotor userData. ----
    seaplane(d){ const g=new THREE.Group();
      const fuse=new THREE.Mesh(new THREE.CapsuleGeometry(0.7,5.4,5,10),MAT.heli); fuse.rotation.x=Math.PI/2; g.add(fuse);
      g.add(box(0.7,0.6,1.1,MAT.glass,0,0.55,2.0));             // cockpit glazing
      const wing=box(9.5,0.24,1.5,MAT.sup,0,0.9,0.2); g.add(wing);
      const tailFin=box(0.12,1.3,1.0,MAT.heli,0,0.9,-4.6); g.add(tailFin);
      const tailPlane=box(2.6,0.12,0.7,MAT.dark,0,0.35,-4.6); g.add(tailPlane);
      const prop=cyl(0.5,0.5,0.08,MAT.dark,16); prop.rotation.x=Math.PI/2; prop.position.set(0,0,3.1); g.add(prop);
      const nose=cyl(0.4,0.55,0.7,MAT.steel,10); nose.rotation.x=Math.PI/2; nose.position.set(0,0,2.7); g.add(nose);
      const strutY=-0.6;
      [-3.2,3.2].forEach(x=>{
        const float=new THREE.Mesh(new THREE.CapsuleGeometry(0.3,3.0,4,8),MAT.dark); float.rotation.x=Math.PI/2; float.position.set(x,-1.6,0.4); g.add(float);
        g.add(box(0.1,1.0,0.1,MAT.steel,x,strutY-0.4,0.4)); });
      const turrets=d.turrets.map(t=>{ const tg=turretGroup(t); tg.pivot.scale.setScalar(0.4); g.add(tg.pivot); return tg; });
      const pick=new THREE.Mesh(new THREE.BoxGeometry(10,2.4,8.5), new THREE.MeshBasicMaterial({visible:false})); g.add(pick);
      return {group:g,turrets,pick}; },

    // ---- 10. Patrol bomber: big straight wing, 2 engine nacelles, twin tail ----
    patrolbomber(d){ const g=new THREE.Group();
      const fuse=new THREE.Mesh(new THREE.CapsuleGeometry(1.1,8.5,6,10),MAT.heli); fuse.rotation.x=Math.PI/2; g.add(fuse);
      g.add(box(1.0,0.9,1.6,MAT.glass,0,0.7,6.0));              // nose glazing/cockpit
      const wing=box(15.5,0.32,2.4,MAT.sup,0,1.1,0.5); g.add(wing);
      [-4.6,4.6].forEach(x=>{
        const nac=cyl(0.55,0.55,2.4,MAT.dark,10); nac.rotation.x=Math.PI/2; nac.position.set(x,1.1,1.7); g.add(nac);
        const prop=cyl(0.45,0.45,0.07,MAT.steel,14); prop.rotation.x=Math.PI/2; prop.position.set(x,1.1,2.95); g.add(prop); });
      // twin tail booms/fins off the trailing edge of the tailplane
      const tailPlane=box(4.6,0.16,1.2,MAT.dark,0,0.8,-7.6); g.add(tailPlane);
      [-2.2,2.2].forEach(x=>{ g.add(box(0.14,1.5,1.0,MAT.heli,x,1.4,-7.8)); });
      const turrets=d.turrets.map(t=>{ const tg=turretGroup(t); tg.pivot.scale.setScalar(0.55); g.add(tg.pivot); return tg; });
      const pick=new THREE.Mesh(new THREE.BoxGeometry(16,3.2,17), new THREE.MeshBasicMaterial({visible:false})); g.add(pick);
      return {group:g,turrets,pick}; },
  };

  return {units, models};
}
