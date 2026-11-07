using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Weapons;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// DOUBLE-JUMP SIDE EFFECTS. The movement code already spends every extra jump the gear grants (<see cref="Abilities"/>.ExtraJumps counts each enabled tModLoader ExtraJump; AirJump / TryJump / the dodge kick all
	/// bump _jumpsUsed). A real player's jump also runs ExtraJump.OnStarted, which for a mod's jump can be gameplay (Calamity's Sulphurous jump spawns a sulphur bubble that inflicts Irradiated). The soldier's dummy never
	/// runs Player.Update, so nothing fired it: the movement-visuals file stood in a plain cloud for every non-vanilla jump and, on a dedicated server, ran nothing at all. This runs the jump's real OnStarted for every
	/// non-vanilla jump the body spends (server / single player), the way a weapon's Shoot hook runs: the dummy stands in the soldier owner slot, and the projectiles the hook spawned are tagged to this soldier.
	/// Vanilla jumps stay cosmetic (dust and sound, client side, in MovementFx).
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>Harness telemetry: jumps spent by type name, projectiles the jump hooks spawned, hooks that threw.</summary>
		internal static readonly Dictionary<string, int> JumpFxCounts = new();
		internal static int JumpFxProj, JumpFxFailed, JumpFxDmg;
		internal static bool JumpFxOn = Environment.GetEnvironmentVariable("LEVY_JUMPFX") != "0";
		private int _jfxSeen;
		private static bool[] _jfxBefore;
		private static FieldInfo _jfxCalCooldown; private static bool _jfxCalLooked;

		/// <summary>Names of the extra jumps the gear enables (harness: what the kit really is).</summary>
		internal string EnabledJumpNames()
		{
			var ordered = ExtraJumpLoader.OrderedJumps;
			if (ordered == null || Dummy == null) return "";
			var names = new List<string>();
			for (int i = 0; i < ordered.Count; i++) { ExtraJump j = ordered[i]; if (j != null && Dummy.GetJumpState(j).Enabled) names.Add(j.GetType().Name); }
			return string.Join("+", names);
		}

		/// <summary>Called every tick after movement on the server / single player: runs the hooks of any extra jump spent since the last tick.</summary>
		private void TickJumpEffects()
		{
			if (!JumpFxOn || Dummy == null || Record == null) return;
			if (_jumpsUsed < _jfxSeen) _jfxSeen = _jumpsUsed; // landed: the jumps refilled
			while (_jfxSeen < _jumpsUsed)
			{
				int idx = _jfxSeen++;
				ExtraJump jump = SpentJump(idx);
				if (jump != null) FireJumpEffects(jump);
			}
		}

		private void FireJumpEffects(ExtraJump jump)
		{
			string name = jump.GetType().Name;
			JumpFxCounts.TryGetValue(name, out int c); JumpFxCounts[name] = c + 1;
			if (jump is VanillaExtraJump) return;
			Player d = Dummy;
			int savedWho = d.whoAmI, savedDir = d.direction; Vector2 savedCenter = d.Center, savedVel = d.velocity;
			bool[] before = _jfxBefore ??= new bool[Main.maxProjectiles];
			for (int i = 0; i < Main.maxProjectiles; i++) before[i] = Main.projectile[i].active;
			try
			{
				d.whoAmI = WeaponUse.OwnerFor(this); d.Center = NPC.Center; d.velocity = NPC.velocity; d.direction = NPC.direction == 0 ? 1 : NPC.direction;
				ClearJumpCooldown(d); // Calamity's bubble cooldown is counted down in its Player update, which the dummy never runs
				bool playSound = false;
				jump.OnStarted(d, ref playSound);
			}
			catch (Exception e) { JumpFxFailed++; Levy.Instance?.Logger.Warn("Levy: extra jump " + name + " hook failed: " + e.Message); }
			finally { d.whoAmI = savedWho; d.direction = savedDir; d.Center = savedCenter; d.velocity = savedVel; }
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || before[i]) continue;
				WeaponUse.TagSoldierProjectile(this, p, null);
				JumpFxProj++; JumpFxDmg += p.damage;
			}
		}

		private static void ClearJumpCooldown(Player d)
		{
			try
			{
				if (!_jfxCalLooked)
				{
					_jfxCalLooked = true;
					foreach (var mp in d.ModPlayers) if (mp.GetType().Name == "CalamityPlayer") { _jfxCalCooldown = mp.GetType().GetField("sulphurBubbleCooldown"); break; }
				}
				if (_jfxCalCooldown == null) return;
				foreach (var mp in d.ModPlayers) if (mp.GetType() == _jfxCalCooldown.DeclaringType) { _jfxCalCooldown.SetValue(mp, 0); break; }
			}
			catch { }
		}
	}
}
