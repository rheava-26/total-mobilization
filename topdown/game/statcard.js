// Hover/select stat card — "cards" meaning a small info panel per unit, NOT a
// deckbuilding mechanic. Shows name + live stats for whichever unit is under
// the cursor or currently selected.
export function createStatCard(el) {
  return {
    show(u, sx, sy) {
      if (!u) { el.style.display = 'none'; return; }
      const d = u.def;
      el.innerHTML = `<b>${d.name}</b>
        <div class="row"><span>HP</span><span>${Math.max(0, Math.round(u.hp))} / ${d.maxHp || d.hp}</span></div>
        <div class="row"><span>Armor</span><span>${d.armor}</span></div>
        <div class="row"><span>Speed</span><span>${d.speed}</span></div>
        <div class="row"><span>Damage</span><span>${d.dmg}</span></div>
        <div class="row"><span>Range</span><span>${d.range}</span></div>
        <div class="row"><span>Domain</span><span>${d.domain}</span></div>`;
      el.style.left = (sx + 16) + 'px';
      el.style.top = (sy + 16) + 'px';
      el.style.display = 'block';
    },
    hide() { el.style.display = 'none'; },
  };
}
