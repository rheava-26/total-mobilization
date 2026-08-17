// fleet-heavy.js — capital & major warship classes (procedural models)
// Same conventions as game.html §G MODELS: ships face +Z, deck top ≈ y 0.1-0.5,
// turret pos = local [x(side), y(above deck), z(fore+)]. Builders run lazily —
// makeFleet(H) itself must not touch THREE or call any builder.
export function makeFleet(H){
  const {THREE, MAT, box, cyl, buildHull, finishShip, turretGroup} = H;

  // ---------------------------------------------------------------------
  // §UNITS — UNIT_DEFS-shaped entries. Weapons use only existing WEAPON_DEFS
  // keys: mainGun, aaGun, autocannon, rocket, lightMG.
  // ---------------------------------------------------------------------
  const units = {
    fletcher:{ name:'Fletcher-class Destroyer', cls:'DESTROYER', model:'fletcher', domain:'sea',
      len:70, beam:8, mass:2500, hp:300,
      turrets:[
        {w:'mainGun', pos:[0,0.9,28],  barrel:5.6},
        {w:'mainGun', pos:[0,2.6,21],  barrel:5.6},           // superfiring — higher, tucked behind #1
        {w:'mainGun', pos:[0,0.9,-27], barrel:5.6},
        {w:'aaGun',   pos:[-2.3,2.0,-4], barrel:2.4},
        {w:'aaGun',   pos:[ 2.3,2.0,-4], barrel:2.4},
      ] },

    gearing:{ name:'Gearing-class Destroyer', cls:'DESTROYER', model:'gearing', domain:'sea',
      len:72, beam:8.4, mass:2600, hp:320,
      turrets:[
        {w:'mainGun', pos:[0,0.9,29],  barrel:5.8, twin:true},
        {w:'mainGun', pos:[0,0.9,-29], barrel:5.8, twin:true},
        {w:'aaGun',   pos:[0,3.1,5],     barrel:2.4},
        {w:'aaGun',   pos:[-2.5,1.6,-11], barrel:2.2},
        {w:'aaGun',   pos:[ 2.5,1.6,-11], barrel:2.2},
      ] },

    iowa:{ name:'Iowa-class Battleship', cls:'BATTLESHIP', model:'iowa', domain:'sea',
      len:120, beam:15, mass:45000, hp:1200,
      turrets:[
        {w:'mainGun', pos:[0,1.2,45],  barrel:10, scale:1.6, twin:true},
        {w:'mainGun', pos:[0,3.4,35],  barrel:10, scale:1.6, twin:true},   // superfiring
        {w:'mainGun', pos:[0,1.2,-41], barrel:10, scale:1.6, twin:true},
        {w:'aaGun',   pos:[-5.2,5.4,11], barrel:2.6},
        {w:'aaGun',   pos:[ 5.2,5.4,11], barrel:2.6},
        {w:'aaGun',   pos:[-5.2,5.4,-15], barrel:2.6},
        {w:'aaGun',   pos:[ 5.2,5.4,-15], barrel:2.6},
        {w:'autocannon', pos:[-6.4,2.0,-1], barrel:2.2},
        {w:'autocannon', pos:[ 6.4,2.0,-1], barrel:2.2},
      ] },

    ticonderoga:{ name:'Ticonderoga-class Cruiser', cls:'CRUISER', model:'ticonderoga', domain:'sea',
      len:80, beam:10, mass:9600, hp:600,
      turrets:[
        {w:'mainGun', pos:[0,0.9,31],  barrel:5.4},
        {w:'mainGun', pos:[0,0.9,-31], barrel:5.4},
        {w:'rocket',  pos:[-2.6,0.75,15], barrel:1.2},
        {w:'rocket',  pos:[ 2.6,0.75,15], barrel:1.2},
        {w:'autocannon', pos:[-3.2,3.4,0], barrel:2.0},
        {w:'autocannon', pos:[ 3.2,3.4,0], barrel:2.0},
      ] },

    perry:{ name:'Oliver Hazard Perry-class Frigate', cls:'FRIGATE', model:'perry', domain:'sea',
      len:60, beam:8, mass:4100, hp:380,
      turrets:[
        {w:'mainGun',    pos:[0,0.9,25], barrel:5.0},
        {w:'rocket',     pos:[0,1.3,15], barrel:1.4},
        {w:'autocannon', pos:[0,2.5,-3], barrel:2.0},
      ] },

    atlanta:{ name:'Atlanta-class AA Cruiser', cls:'AA CRUISER', model:'atlanta', domain:'sea',
      len:75, beam:9, mass:6700, hp:500,
      turrets:[
        {w:'mainGun', pos:[0,0.9,29],  barrel:5.2},
        {w:'mainGun', pos:[0,0.9,-29], barrel:5.2},
        {w:'aaGun', pos:[-2.7,1.7,17],  barrel:2.4, twin:true},
        {w:'aaGun', pos:[ 2.7,1.7,17],  barrel:2.4, twin:true},
        {w:'aaGun', pos:[-2.7,1.7,-15], barrel:2.4, twin:true},
        {w:'aaGun', pos:[ 2.7,1.7,-15], barrel:2.4, twin:true},
      ] },

    essex:{ name:'Essex-class Carrier', cls:'CARRIER', model:'essex', domain:'sea',
      len:118, beam:14, mass:36000, hp:1000,
      turrets:[
        {w:'aaGun', pos:[-6.8,4.6,42],  barrel:2.2},
        {w:'aaGun', pos:[-6.8,4.6,-42], barrel:2.2},
        {w:'aaGun', pos:[ 6.8,4.6,0],   barrel:2.2},
      ] },

    oiler:{ name:'Fleet Oiler', cls:'SUPPORT', model:'oiler', domain:'sea',
      len:85, beam:12, mass:12000, hp:450,
      turrets:[
        {w:'aaGun',   pos:[0,2.0,-32], barrel:2.2},
        {w:'lightMG', pos:[0,1.0,32],  barrel:1.6},
      ] },
  };

  // ---------------------------------------------------------------------
  // §MODELS — procedural builders. Each returns {group, turrets, pick} via
  // finishShip(g,d), which also instantiates d.turrets through turretGroup.
  // ---------------------------------------------------------------------
  const models = {

    // Fletcher: flush low-freeboard hull, twin slim funnels, single lattice mast,
    // 2 fore superfiring mounts + 1 aft (silhouette: lean, two stacks close together).
    fletcher(d){ const g=buildHull(d), B=d.beam, L=d.len;
      g.add(box(B*0.55,1.6,L*0.16, MAT.sup, 0,0.9,L*0.09));            // fwd bridge deckhouse
      g.add(box(B*0.42,1.0,1.3, MAT.glass, 0,1.9,L*0.14));             // bridge windows
      const fn1=cyl(0.5,0.62,2.6,MAT.dark,12); fn1.position.set(0,2.2,-L*0.02); g.add(fn1);
      const fn2=cyl(0.55,0.68,2.9,MAT.dark,12); fn2.position.set(0,2.35,-L*0.11); g.add(fn2);
      g.add(box(B*0.4,1.0,L*0.1, MAT.sup, 0,0.85,-L*0.24));            // aft deckhouse
      g.add(box(0.14,4.6,0.14,MAT.steel,0,3.5,L*0.16));                // mast
      g.add(box(2.0,0.1,0.1,MAT.steel,-1,4.4,L*0.16));
      return finishShip(g,d); },

    // Gearing: single big funnel + smaller aft funnel, tripod mast, raised aft deckhouse
    // (post-war refit look), twin superimposed main mounts fore/aft.
    gearing(d){ const g=buildHull(d), B=d.beam, L=d.len;
      g.add(box(B*0.6,1.9,L*0.2, MAT.sup, 0,1.05,L*0.06));
      g.add(box(B*0.46,1.0,1.4, MAT.glass, 0,2.2,L*0.12));
      const fn1=cyl(0.62,0.78,3.2,MAT.dark,12); fn1.position.set(0,2.5,-L*0.03); g.add(fn1);
      const fn2=cyl(0.45,0.56,2.2,MAT.dark,12); fn2.position.set(0,2.1,-L*0.16); g.add(fn2);
      g.add(box(B*0.5,1.3,L*0.12, MAT.supHi, 0,1.05,-L*0.28));         // raised aft house
      // tripod mast (3 legs converging)
      g.add(box(0.14,5.2,0.14,MAT.steel,0,3.8,L*0.1));
      [-1,1].forEach(s=>{ const leg=box(0.1,5.4,0.1,MAT.steel,s*0.9,3.6,L*0.1+s*0.5); leg.rotation.z=s*0.12; g.add(leg); });
      return finishShip(g,d); },

    // Iowa: massive armored superstructure block, twin funnels, tall pagoda-style
    // tower mast, thick belt strakes — reads as a battleship at a glance.
    iowa(d){ const g=buildHull(d), B=d.beam, L=d.len;
      [-1,1].forEach(s=> g.add(box(0.25,1.4,L*0.8, MAT.dark, s*B*0.5, 0.4, 0)));  // armor belt
      g.add(box(B*0.72,3.4,L*0.22, MAT.sup, 0,2.0,L*0.1));              // main superstructure block
      g.add(box(B*0.56,2.2,L*0.12, MAT.supHi, 0,4.3,L*0.06));           // bridge tier
      g.add(box(B*0.36,1.6,2.6, MAT.glass, 0,5.6,L*0.1));               // conning windows
      const fn1=cyl(1.3,1.6,3.6,MAT.dark,16); fn1.position.set(0,3.2,-L*0.06); g.add(fn1);
      const fn2=cyl(1.15,1.45,3.2,MAT.dark,16); fn2.position.set(0,3.0,-L*0.16); g.add(fn2);
      // pagoda tower mast
      g.add(box(1.6,1.0,1.6,MAT.supHi,0,7.0,L*0.12));
      g.add(box(1.1,0.9,1.1,MAT.supHi,0,8.0,L*0.12));
      g.add(box(0.2,3.0,0.2,MAT.steel,0,9.6,L*0.12));
      [-1,1].forEach(s=> g.add(box(1.3,0.6,3.6, MAT.supHi, s*B*0.36,2.2,L*0.02)));  // casemate sponsons
      return finishShip(g,d); },

    // Ticonderoga: boxy flat-sided AEGIS deckhouse with angled SPY radar faces,
    // lattice mast, VLS boxes handled by rocket turrets on deck.
    ticonderoga(d){ const g=buildHull(d), B=d.beam, L=d.len;
      g.add(box(B*0.62,2.6,L*0.34, MAT.sup, 0,1.6,L*0.02));            // tall boxy AEGIS house
      g.add(box(B*0.5,1.5,L*0.14, MAT.supHi, 0,3.3,L*0.14));           // bridge tier forward
      const spyF=box(1.6,1.6,0.15,MAT.dark, B*0.32,2.6,L*0.19); spyF.rotation.y=0.5; g.add(spyF);
      const spyA=box(1.6,1.6,0.15,MAT.dark,-B*0.32,2.6,L*0.19); spyA.rotation.y=-0.5; g.add(spyA);
      const fn=cyl(0.85,1.0,2.6,MAT.dark,14); fn.position.set(0,2.9,-L*0.12); g.add(fn);
      g.add(box(0.16,5.6,0.16,MAT.steel,0,4.4,-L*0.02));               // lattice mast
      g.add(box(2.0,0.12,0.12,MAT.steel,0,5.6,-L*0.02));
      return finishShip(g,d); },

    // Perry: light single-mast frigate, small bridge, prominent flight-deck +
    // hangar box aft for its helicopter detachment.
    perry(d){ const g=buildHull(d), B=d.beam, L=d.len;
      g.add(box(B*0.5,1.4,L*0.18, MAT.sup, 0,0.85,L*0.1));
      g.add(box(B*0.36,0.75,1.2, MAT.glass, 0,1.7,L*0.14));
      const fn=cyl(0.55,0.68,2.2,MAT.dark,12); fn.position.set(0,1.9,-L*0.05); g.add(fn);
      g.add(box(0.12,3.2,0.12,MAT.steel,0,2.4,L*0.06));                // mast
      g.add(box(B*0.62,1.3,L*0.16, MAT.supHi, 0,0.85,-L*0.3));         // hangar box aft
      g.add(box(B*0.7,0.06,L*0.16, MAT.dark, 0,0.42,-L*0.4));          // flight deck marking
      return finishShip(g,d); },

    // Atlanta: dense AA bristle — many small twin mounts around a compact
    // superstructure, two thin funnels, minimal deckhouse mass.
    atlanta(d){ const g=buildHull(d), B=d.beam, L=d.len;
      g.add(box(B*0.5,1.7,L*0.18, MAT.sup, 0,1.0,L*0.02));
      g.add(box(B*0.38,0.9,1.2, MAT.glass, 0,2.1,L*0.06));
      const fn1=cyl(0.55,0.68,2.6,MAT.dark,12); fn1.position.set(0,2.3,-L*0.06); g.add(fn1);
      const fn2=cyl(0.5,0.62,2.3,MAT.dark,12); fn2.position.set(0,2.1,-L*0.15); g.add(fn2);
      g.add(box(0.14,4.0,0.14,MAT.steel,0,3.3,L*0.04));
      [-1,1].forEach(s=> g.add(box(0.9,1.0,0.9, MAT.accent, s*B*0.4,1.4,L*0.2)));  // small AA sponsons
      return finishShip(g,d); },

    // Essex: full-length flat flight deck raised on hangar sides, small island
    // offset to starboard, angled funnel, a few parked-aircraft decoration boxes.
    essex(d){ const g=buildHull(d), B=d.beam, L=d.len;
      const deckY=3.4;
      [-1,1].forEach(s=> g.add(box(0.3,deckY-0.3,L*0.92, MAT.hullLo, s*B*0.47, deckY*0.5, 0)));  // hangar sides
      g.add(box(B*0.98,0.4,L*0.96, MAT.deck, 0, deckY, 0));            // flight deck
      // island, offset to starboard (+x)
      const ix=B*0.34;
      g.add(box(1.8,2.6,3.2, MAT.sup, ix, deckY+1.5, L*0.05));
      g.add(box(1.5,1.2,1.6, MAT.glass, ix, deckY+2.9, L*0.05));
      const fn=cyl(0.55,0.7,2.6,MAT.dark,12); fn.position.set(ix,deckY+2.6,-L*0.02); fn.rotation.z=0.12; g.add(fn);
      g.add(box(0.12,3.4,0.12,MAT.steel, ix,deckY+4.6,L*0.07));        // island mast
      // parked-aircraft decorations (tiny flat boxes on deck)
      [[-3.5,L*0.28],[-2.5,L*0.1],[-4,-L*0.22]].forEach(([px,pz])=>{
        g.add(box(0.9,0.22,2.6, MAT.dark, px, deckY+0.32, pz));
      });
      return finishShip(g,d); },

    // Oiler: long low deck, mostly unarmed hauler — paired kingposts (goalpost
    // masts) fore and aft, thin deck pipes running the length, small bridge aft.
    oiler(d){ const g=buildHull(d), B=d.beam, L=d.len;
      g.add(box(B*0.5,1.5,L*0.14, MAT.sup, 0,0.9,-L*0.33));            // small aft bridge
      g.add(box(B*0.36,0.8,1.1, MAT.glass, 0,1.75,-L*0.29));
      const fn=cyl(0.55,0.68,2.0,MAT.dark,12); fn.position.set(0,1.85,-L*0.4); g.add(fn);
      // paired kingposts (goalpost masts), fore and aft cargo stations
      [L*0.18, -L*0.05].forEach(pz=>{
        [-1,1].forEach(s=> g.add(box(0.16,3.2,0.16,MAT.steel, s*B*0.32,1.8,pz)));
        g.add(box(B*0.66,0.14,0.14,MAT.steel, 0,3.3,pz));
      });
      // deck pipes running fore-aft
      [-1,1].forEach(s=>{ const p=cyl(0.1,0.1,L*0.6,MAT.dark,8); p.rotation.x=Math.PI/2; p.position.set(s*B*0.14,0.55,0); g.add(p); });
      return finishShip(g,d); },
  };

  return {units, models};
}
