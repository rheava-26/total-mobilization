# Biome Blades in levy hands (lab note)

Specs: `bb_attune.yaml` (each attunement forced with `bb=`), `bb_biomes.yaml` (the shipped behaviour: the commander stands in a biome, `zone:`; `zswap: "hell@15"` moves him 15 s in).
Setup: King Slime x100 life, 16 levies each carrying the one weapon (postskel.best), commander far, 40 s, 3 seeds. Numbers are weapon damage per levy-second (`weaponDmg / (n x avgSurvSec)`); seeds vary by about 25%.
Raw tables: `attunements_dps.txt`, `biomes_dps.txt`.

- Every attunement of every blade fires (counters `BB-FIGHT` with LEVY_BB_STATS=1), zero exceptions from the blades. A biome change mid-fight re-attunes (Cold -> Hot, TrueTropical -> Holy, FlailBlade -> SuperPogo) and the previous attunement stays as the secondary: with a FlailBlade secondary the Omega's passive (meat hook twirl on proc) runs (`passives=`).
- Broken 20-40, Biome Blade 80-190, Omega 55-290, Galaxia 330-860 per levy-second; same run: Muramasa 20, Excalibur 77-130, Terra Blade 160-190, True Ark 140-210, Exoblade 3,300-4,600.
- Weak: Omega FlailBlade (55) and Shockwave in some seeds. Left as measured; the ranking numbers (`LevyConstants.BiomeSustainedMult`) are the measured share over the same ratio for plain swords (~0.43).
