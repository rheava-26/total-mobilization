// GAME PHASES (P4 tutorial substrate — CONCEPT.md's settled "Phases" line:
// "a run has a PREPARATION phase (build-up, fog off) and a COMBAT phase (fog
// on, the enemy arrives)"). This file owns ONLY the phase state and the one
// place phase reconciles to fog (Pillar 3's fog bullet). It does NOT build
// the guided tutorial, the scripted encounter, or an established starting
// industry — those are a later agent's job; this is just the on/off switch
// and the wire from it to fog, plus a manual override lever.
//
// Two phases for now — prep -> combat is one-way (per the task: "no need to
// return to prep"). A future tutorial/replay pass can add more phases or a
// return path; this module doesn't assume prep->combat is the only legal
// transition, it just doesn't offer a combat->prep control today.

export const PHASES = { PREP: 'prep', COMBAT: 'combat' };

export function createPhaseState() {
  return { phase: PHASES.PREP };
}

// THE single reconciliation point: phase -> fog. Anything that changes phase
// MUST go through here (setPhase/beginCombat below) rather than poking
// fogState directly, so the two systems can never drift apart.
//
// Manual fog toggle (F, see main.js) writes fogState.revealAll directly and
// nothing else touches it every frame — so "the player's manual choice
// sticks until the next phase change" falls out for free from this being
// the ONLY other writer, called ONLY on an actual phase transition. No extra
// "is this overridden" bookkeeping needed.
export function applyPhaseFog(phase, fogState) {
  fogState.revealAll = (phase === PHASES.PREP);
}

// Generic phase setter (used by beginCombat below; kept generic rather than
// hardcoding "combat" in case a later phase gets added). No-ops and returns
// false if already in that phase, so callers can tell whether a transition
// (and therefore an announcement) actually happened.
export function setPhase(phaseState, phase, fogState) {
  if (phaseState.phase === phase) return false;
  phaseState.phase = phase;
  applyPhaseFog(phase, fogState);
  return true;
}

export function beginCombat(phaseState, fogState) {
  return setPhase(phaseState, PHASES.COMBAT, fogState);
}
