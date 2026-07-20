// Hover/select stat card — "cards" meaning a small info panel per unit, NOT a
// deckbuilding mechanic. Shows name + live stats for whichever unit is under
// the cursor or currently selected. Also surfaces the ATTRIBUTE SYSTEM
// (move class, weapon + its targeting scope) so the data-driven layers
// backing the unit are visible to the player, not just their end effects.
import { WEAPON_DEFS } from './units.js';

function weaponLabel(weaponName) {
  const w = WEAPON_DEFS[weaponName];
  if (!w) return weaponName;
  const scope = w.canTargetAir && w.canTargetGround ? 'air + ground'
    : w.canTargetAir ? 'air only'
    : w.canTargetGround ? 'ground only'
    : 'none';
  return `${weaponName.toUpperCase()} — ${scope}`;
}

// A building def (game/buildings.js) carries `footprint`; a unit def never
// does — that's the one attribute check needed to branch the card layout,
// no separate "isBuilding" flag required on the entity itself.
function buildingCardHtml(u, d) {
  let html = `<b>${d.name}</b>
    <div class="row"><span>HP</span><span>${Math.max(0, Math.round(u.hp))} / ${d.maxHp || d.hp}</span></div>
    <div class="row"><span>Footprint</span><span>${d.footprint.w}×${d.footprint.h}</span></div>
    <div class="row"><span>Vision</span><span>${d.vision}</span></div>`;
  if (d.weapon) {
    html += `<div class="row"><span>Weapon</span><span>${weaponLabel(d.weapon)}</span></div>
      <div class="row"><span>Damage</span><span>${d.dmg}</span></div>
      <div class="row"><span>Range</span><span>${d.range}</span></div>`;
  }
  return html;
}

function unitCardHtml(u, d) {
  return `<b>${d.name}</b>
    <div class="row"><span>HP</span><span>${Math.max(0, Math.round(u.hp))} / ${d.maxHp || d.hp}</span></div>
    <div class="row"><span>Armor</span><span>${d.armor}</span></div>
    <div class="row"><span>Speed</span><span>${d.speed}</span></div>
    <div class="row"><span>Damage</span><span>${d.dmg}</span></div>
    <div class="row"><span>Range</span><span>${d.range}</span></div>
    <div class="row"><span>Domain</span><span>${d.domain}</span></div>
    <div class="row"><span>Move class</span><span>${d.moveClass}</span></div>
    <div class="row"><span>Weapon</span><span>${weaponLabel(d.weapon)}</span></div>`;
}

export function createStatCard(el) {
  return {
    show(u, sx, sy) {
      if (!u) { el.style.display = 'none'; return; }
      const d = u.def;
      el.innerHTML = d.footprint ? buildingCardHtml(u, d) : unitCardHtml(u, d);
      el.style.left = (sx + 16) + 'px';
      el.style.top = (sy + 16) + 'px';
      el.style.display = 'block';
    },
    hide() { el.style.display = 'none'; },
  };
}
