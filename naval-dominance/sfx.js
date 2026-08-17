// sfx.js — procedural WebAudio sound bank (synth approved by design)
// Layered synthesis: transient crack + filtered body + sub-bass thump + tail.
// The game owns the AudioContext/master gain; this module only builds nodes
// against them. Every method is a safe no-op when ctx is null (not init'd).

export function makeSFX(){
  let ctx=null, master=null;
  let noiseBuf=null;       // shared white-noise buffer (1s), reused via BufferSource per play
  let ambienceNode=null;   // guards against double-starting ambience

  // ---- helpers ----------------------------------------------------------
  function noiseSrc(){
    const s=ctx.createBufferSource();
    s.buffer=noiseBuf;
    s.loop=true; // callers stop() it before the 1s buffer would audibly repeat
    return s;
  }
  function jitter(base,amt){ return base*(1+(Math.random()*2-1)*amt); }
  function now(){ return ctx.currentTime; }

  // simple gain envelope: attack to `peak` over `atk`s, then exponential
  // decay toward ~0 over `dec`s starting at `atk`.
  function envGain(peak,atk,dec,startAt){
    const g=ctx.createGain();
    const t0=startAt;
    g.gain.setValueAtTime(0.0001,t0);
    g.gain.exponentialRampToValueAtTime(Math.max(peak,0.0001),t0+Math.max(atk,0.001));
    g.gain.exponentialRampToValueAtTime(0.0001,t0+atk+Math.max(dec,0.001));
    return g;
  }

  return {
    init(audioCtx, masterGain){
      ctx=audioCtx; master=masterGain;
      if(!ctx) return;
      // one shared white-noise buffer, reused as the source for every
      // burst/tail sound below (createBufferSource per play, per contract).
      const len=Math.floor(ctx.sampleRate*1.0);
      noiseBuf=ctx.createBuffer(1,len,ctx.sampleRate);
      const d=noiseBuf.getChannelData(0);
      for(let i=0;i<len;i++) d[i]=Math.random()*2-1;
    },

    // -- gunshot: crack (bright noise burst) + body (bandpass mid boom)
    //    + sub thump (falling sine) + short lowpassed tail. Bigger caliber
    //    = slower attack, deeper body/sub, longer tail. -----------------
    gunshot(caliber){
      if(!ctx) return;
      const t=now();
      const cal=caliber==='large'?'large':caliber==='medium'?'medium':'small';
      const pitchJ=1+(Math.random()*2-1)*0.06;   // per-shot pitch jitter
      const gainJ=1+(Math.random()*2-1)*0.12;    // per-shot gain jitter

      if(cal==='small'){
        // -- crack: 60-110ms bright noise burst, highpassed 1.5-3kHz --
        const dur=jitter(0.08,0.25);
        const n=noiseSrc();
        const hp=ctx.createBiquadFilter(); hp.type='highpass'; hp.frequency.value=jitter(2200,0.15)*pitchJ;
        const g=envGain(0.5*gainJ,0.002,dur,t);
        n.connect(hp).connect(g).connect(master);
        n.start(t); n.stop(t+dur+0.05);
        // -- tiny mid body, light --
        const n2=noiseSrc();
        const bp=ctx.createBiquadFilter(); bp.type='bandpass'; bp.frequency.value=700*pitchJ; bp.Q.value=1.1;
        const g2=envGain(0.18*gainJ,0.003,0.05,t);
        n2.connect(bp).connect(g2).connect(master);
        n2.start(t); n2.stop(t+0.08);
        return;
      }

      if(cal==='medium'){
        // -- crack --
        const n=noiseSrc();
        const hp=ctx.createBiquadFilter(); hp.type='highpass'; hp.frequency.value=jitter(1800,0.15)*pitchJ;
        const g=envGain(0.55*gainJ,0.003,0.07,t);
        n.connect(hp).connect(g).connect(master);
        n.start(t); n.stop(t+0.12);
        // -- body: bandpass ~250-700Hz, ~200ms, staggered a few ms --
        const t1=t+0.004;
        const n2=noiseSrc();
        const bp=ctx.createBiquadFilter(); bp.type='bandpass'; bp.frequency.value=jitter(420,0.15)*pitchJ; bp.Q.value=0.9;
        const g2=envGain(0.5*gainJ,0.006,0.2,t1);
        n2.connect(bp).connect(g2).connect(master);
        n2.start(t1); n2.stop(t1+0.24);
        // -- sub thump: sine 110->45Hz, ~250ms --
        const t2=t+0.006;
        const o=ctx.createOscillator(); o.type='sine';
        o.frequency.setValueAtTime(110*pitchJ,t2);
        o.frequency.exponentialRampToValueAtTime(45*pitchJ,t2+0.25);
        const og=envGain(0.7*gainJ,0.005,0.26,t2);
        o.connect(og).connect(master);
        o.start(t2); o.stop(t2+0.3);
        // -- short lowpassed tail --
        const t3=t+0.02;
        const n3=noiseSrc();
        const lp=ctx.createBiquadFilter(); lp.type='lowpass'; lp.frequency.value=900*pitchJ;
        const g3=envGain(0.22*gainJ,0.01,0.22,t3);
        n3.connect(lp).connect(g3).connect(master);
        n3.start(t3); n3.stop(t3+0.26);
        return;
      }

      // -- large: slower attack broadside crack + big body + strong sub --
      const n=noiseSrc();
      const hp=ctx.createBiquadFilter(); hp.type='highpass'; hp.frequency.value=jitter(1400,0.15)*pitchJ;
      const g=envGain(0.62*gainJ,0.006,0.1,t);
      n.connect(hp).connect(g).connect(master);
      n.start(t); n.stop(t+0.16);
      // -- big body: bandpass ~120-400Hz, staggered --
      const t1=t+0.008;
      const n2=noiseSrc();
      const bp=ctx.createBiquadFilter(); bp.type='bandpass'; bp.frequency.value=jitter(220,0.18)*pitchJ; bp.Q.value=0.7;
      const g2=envGain(0.6*gainJ,0.015,0.36,t1);
      n2.connect(bp).connect(g2).connect(master);
      n2.start(t1); n2.stop(t1+0.4);
      // -- strong sub: sine 80->35Hz, ~450ms --
      const t2=t+0.01;
      const o=ctx.createOscillator(); o.type='sine';
      o.frequency.setValueAtTime(80*pitchJ,t2);
      o.frequency.exponentialRampToValueAtTime(35*pitchJ,t2+0.45);
      const og=envGain(0.85*gainJ,0.012,0.46,t2);
      o.connect(og).connect(master);
      o.start(t2); o.stop(t2+0.5);
      // second detuned sine for extra weight
      const o2=ctx.createOscillator(); o2.type='triangle';
      o2.frequency.setValueAtTime(78*pitchJ,t2);
      o2.frequency.exponentialRampToValueAtTime(33*pitchJ,t2+0.45);
      const og2=envGain(0.3*gainJ,0.012,0.46,t2);
      o2.connect(og2).connect(master);
      o2.start(t2); o2.stop(t2+0.5);
      // -- long lowpassed noise tail, feels like a broadside --
      const t3=t+0.03;
      const n3=noiseSrc();
      const lp=ctx.createBiquadFilter(); lp.type='lowpass'; lp.frequency.value=600*pitchJ;
      const g3=envGain(0.32*gainJ,0.02,0.4,t3);
      n3.connect(lp).connect(g3).connect(master);
      n3.start(t3); n3.stop(t3+0.45);
    },

    // -- impact: metallic ping (high bandpass noise, quick decay) +
    //    a couple ms-scale clicks. -------------------------------------
    impact(){
      if(!ctx) return;
      const t=now();
      const pitchJ=1+(Math.random()*2-1)*0.1;
      // metallic ping
      const n=noiseSrc();
      const bp=ctx.createBiquadFilter(); bp.type='bandpass'; bp.frequency.value=3200*pitchJ; bp.Q.value=6;
      const g=envGain(0.5,0.001,0.09,t);
      n.connect(bp).connect(g).connect(master);
      n.start(t); n.stop(t+0.12);
      // a couple of ms-scale clicks
      for(let i=0;i<2;i++){
        const tc=t+0.01+i*0.018;
        const nc=noiseSrc();
        const hp=ctx.createBiquadFilter(); hp.type='highpass'; hp.frequency.value=5000*pitchJ;
        const gc=envGain(0.22,0.0005,0.012,tc);
        nc.connect(hp).connect(gc).connect(master);
        nc.start(tc); nc.stop(tc+0.02);
      }
      // spark fizz: short bright bandpass sizzle
      const tf=t+0.005;
      const nf=noiseSrc();
      const bpf=ctx.createBiquadFilter(); bpf.type='bandpass'; bpf.frequency.value=6000*pitchJ; bpf.Q.value=1.4;
      const gf=envGain(0.16,0.005,0.15,tf);
      nf.connect(bpf).connect(gf).connect(master);
      nf.start(tf); nf.stop(tf+0.18);
    },

    // -- explosion: lowpassed noise swell + deep falling sine + rumble tail
    explosion(){
      if(!ctx) return;
      const t=now();
      // lowpassed noise swell (fast-ish attack, big decay)
      const n=noiseSrc();
      const lp=ctx.createBiquadFilter(); lp.type='lowpass'; lp.frequency.value=1200;
      const g=envGain(0.75,0.01,0.35,t);
      n.connect(lp).connect(g).connect(master);
      n.start(t); n.stop(t+0.4);
      // deep sine 70->28Hz
      const o=ctx.createOscillator(); o.type='sine';
      o.frequency.setValueAtTime(70,t);
      o.frequency.exponentialRampToValueAtTime(28,t+0.5);
      const og=envGain(0.85,0.015,0.55,t);
      o.connect(og).connect(master);
      o.start(t); o.stop(t+0.6);
      // 0.6s rumble tail: lowpassed noise, slow decay, filter sweeps down
      const t1=t+0.05;
      const n2=noiseSrc();
      const lp2=ctx.createBiquadFilter(); lp2.type='lowpass';
      lp2.frequency.setValueAtTime(500,t1);
      lp2.frequency.exponentialRampToValueAtTime(90,t1+0.6);
      const g2=envGain(0.5,0.02,0.6,t1);
      n2.connect(lp2).connect(g2).connect(master);
      n2.start(t1); n2.stop(t1+0.65);
    },

    // -- splash: bandpassed noise ~400-1200Hz, fast attack, wobbly decay
    //    via LFO on filter frequency. ------------------------------------
    splash(){
      if(!ctx) return;
      const t=now();
      const n=noiseSrc();
      const bp=ctx.createBiquadFilter(); bp.type='bandpass';
      bp.frequency.value=800; bp.Q.value=0.8;
      // LFO wobbling the bandpass center between ~400-1200Hz
      const lfo=ctx.createOscillator(); lfo.type='sine'; lfo.frequency.value=14;
      const lfoGain=ctx.createGain(); lfoGain.gain.value=400;
      lfo.connect(lfoGain).connect(bp.frequency);
      const g=envGain(0.45,0.004,0.32,t);
      n.connect(bp).connect(g).connect(master);
      lfo.start(t); lfo.stop(t+0.4);
      n.start(t); n.stop(t+0.4);
      // light high-frequency spray on top
      const t1=t+0.01;
      const n2=noiseSrc();
      const hp=ctx.createBiquadFilter(); hp.type='highpass'; hp.frequency.value=2500;
      const g2=envGain(0.18,0.003,0.12,t1);
      n2.connect(hp).connect(g2).connect(master);
      n2.start(t1); n2.stop(t1+0.15);
    },

    // -- rocket: noise through a rising-then-falling bandpass sweep, ~0.5s
    rocket(){
      if(!ctx) return;
      const t=now();
      const n=noiseSrc();
      const bp=ctx.createBiquadFilter(); bp.type='bandpass'; bp.Q.value=1.2;
      bp.frequency.setValueAtTime(300,t);
      bp.frequency.linearRampToValueAtTime(2200,t+0.18);
      bp.frequency.exponentialRampToValueAtTime(250,t+0.5);
      const g=ctx.createGain();
      g.gain.setValueAtTime(0.0001,t);
      g.gain.exponentialRampToValueAtTime(0.45,t+0.06);
      g.gain.setValueAtTime(0.45,t+0.28);
      g.gain.exponentialRampToValueAtTime(0.0001,t+0.5);
      n.connect(bp).connect(g).connect(master);
      n.start(t); n.stop(t+0.52);
      // faint sub thump under the launch
      const o=ctx.createOscillator(); o.type='sine';
      o.frequency.setValueAtTime(95,t);
      o.frequency.exponentialRampToValueAtTime(50,t+0.2);
      const og=envGain(0.3,0.01,0.2,t);
      o.connect(og).connect(master);
      o.start(t); o.stop(t+0.25);
    },

    // -- ambience: low-volume looping ocean-wash/engine-hum bed. Safe to
    //    call once; returns {stop()} (or a no-op stop if ctx missing). ---
    ambience(){
      if(!ctx){ return { stop(){} }; }
      if(ambienceNode){ return ambienceNode; } // already running; don't double-start

      const n=noiseSrc();
      n.loop=true;
      const lp=ctx.createBiquadFilter(); lp.type='lowpass'; lp.frequency.value=300;
      const g=ctx.createGain(); g.gain.value=0.05;
      // very slow gain LFO for a breathing ocean-wash feel
      const lfo=ctx.createOscillator(); lfo.type='sine'; lfo.frequency.value=0.07;
      const lfoGain=ctx.createGain(); lfoGain.gain.value=0.018;
      lfo.connect(lfoGain).connect(g.gain);

      // low engine-hum tone under the wash
      const hum=ctx.createOscillator(); hum.type='sine'; hum.frequency.value=55;
      const humGain=ctx.createGain(); humGain.gain.value=0.02;

      n.connect(lp).connect(g).connect(master);
      hum.connect(humGain).connect(master);

      const t=now();
      n.start(t); lfo.start(t); hum.start(t);

      let stopped=false;
      ambienceNode={
        stop(){
          if(stopped) return;
          stopped=true;
          try{ n.stop(); }catch(e){}
          try{ lfo.stop(); }catch(e){}
          try{ hum.stop(); }catch(e){}
          ambienceNode=null;
        }
      };
      return ambienceNode;
    },
  };
}
