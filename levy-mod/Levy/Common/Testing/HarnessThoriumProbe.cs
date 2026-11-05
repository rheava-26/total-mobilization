using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using ThoriumMod;
using Levy.Common.Compat.Thorium;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>The Thorium-touching half of the combat harness (JIT-gated so it never resolves a Thorium type when Thorium is absent). Read-only probes.</summary>
	[JITWhenModsEnabled("ThoriumMod")]
	public static class HarnessThoriumProbe
	{
		/// <summary>"bard", "healer:&lt;role&gt;" (Levy's HealerRole) or "".</summary>
		public static string KindOf(Item it)
		{
			if (BardCompat.IsBardWeapon(it)) return "bard";
			HealerRole r = HealerCompat.RoleOf(it);
			return r != HealerRole.None ? "healer:" + r : "";
		}

		/// <summary>Current Inspiration of a bard soldier (on his stat dummy's ThoriumPlayer), -1 when unreadable.</summary>
		public static int BardResource(SoldierNPC s)
		{
			try { return s?.Dummy == null ? -1 : s.Dummy.GetModPlayer<ThoriumPlayer>().bardResource; } catch (Exception) { return -1; }
		}

		public static int BardMax(SoldierNPC s)
		{
			try { return s?.Dummy == null ? -1 : s.Dummy.GetModPlayer<ThoriumPlayer>().bardResourceMax2; } catch (Exception) { return -1; }
		}

		/// <summary>Drops a Thorium healing orb at <paramref name="pos"/> (HealingOrbYellow), owner 0, no damage; returns it. The caller tags it as a soldier's when a soldier 'cast' it.</summary>
		public static Projectile SpawnOrb(Vector2 pos, SoldierNPC caster)
		{
			int type = ModContent.ProjectileType<ThoriumMod.Projectiles.Healer.HealingOrbYellow>();
			Projectile q;
			if (caster != null) q = global::Levy.Common.Weapons.WeaponUse.Spawn(caster, pos, Vector2.Zero, type, 1, 0f, null, 0f, 0f);
			else { int i = Projectile.NewProjectile(null, pos, Vector2.Zero, type, 0, 0f, 0); q = i >= 0 && i < Main.maxProjectiles ? Main.projectile[i] : null; }
			if (q != null) { q.friendly = false; q.damage = 0; }
			return q;
		}

		private static Item _bagItem;
		internal static string DriveErr = "";
		internal static int DriveTicks, DrivePick, DriveVisible, DriveAimed, DriveBoth, DriveBoost;
		private static int _pickTicks, _alphaTicks, _angleTicks; private static readonly System.Collections.Generic.HashSet<int> _symIds = new();
		/// <summary>
		/// The stand-in commander on a dedicated server never runs its ModPlayer hooks, so drive the Medical Bag's by hand each tick, in the order the game does: the accessory flags (UpdateAccessory), the most-wounded pick
		/// (PreUpdate -> HandleLowestLifeAlly), the equipment pass (PostUpdateEquips: the run boost), and keep the Head Mirror's healer symbol alive. Everything is Thorium's own code.
		/// </summary>
		public static string MedBagDrive(Player p)
		{
			string err = "";
			int savedMy = Main.myPlayer; Main.myPlayer = p.whoAmI; // as on the commander's own client
			try
			{
				p.active = true; p.dead = false; // the server resets a client-less slot during the update; the stand-in is re-asserted only at the end of the tick
				var tp = p.GetModPlayer<ThoriumPlayer>();
				if (_bagItem == null) { _bagItem = new Item(); _bagItem.SetDefaults(ModContent.ItemType<ThoriumMod.Items.HealerItems.MedicalBag>()); }
				tp.accMedicalBag = true; tp.headMirror.Set(_bagItem); p.direction = -1; // faces the wounded ally (he stands to the left)
				typeof(ThoriumPlayer).GetMethod("HandleLowestLifeAlly", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(tp, null);
				p.ownedProjectileCounts[ModContent.ProjectileType<ThoriumMod.Projectiles.Healer.HealerSymbol>()] = 1; // a stand-in's projectile counts are never recounted; without this Thorium would spawn a new symbol every tick
				try { tp.PostUpdateEquips(); } catch (Exception e) { err += "PostUpdateEquips:" + (e.InnerException ?? e).GetType().Name + ";"; }
				bool have = false;
				for (int i = 0; i < Main.maxProjectiles; i++) { Projectile q = Main.projectile[i]; if (q.active && q.owner == p.whoAmI && q.ModProjectile is ThoriumMod.Projectiles.Healer.HealerSymbol) { have = true; break; } }
				if (!have) Projectile.NewProjectile(null, p.Center, Vector2.Zero, ModContent.ProjectileType<ThoriumMod.Projectiles.Healer.HealerSymbol>(), 0, 0f, p.whoAmI);
				// run the symbol's own AI right now (Thorium's code, with our postfix), so what it shows is read in the same instant the pick and the Head Mirror were set
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile q = Main.projectile[i];
					if (!q.active || q.owner != p.whoAmI || q.ModProjectile is not ThoriumMod.Projectiles.Healer.HealerSymbol) continue;
					q.ModProjectile.AI();
					DriveTicks++;
					int pk = MedicalBagHook.PickOf(p.whoAmI);
					if (pk >= 0)
					{
						DrivePick++;
						NPC n = Main.npc[pk];
						float bearing = (n.Center - q.Center + new Vector2(0f, q.gfxOffY)).ToRotation();
						if (q.alpha == 0) DriveVisible++;
						if (Math.Abs(MathHelper.WrapAngle(q.rotation - bearing)) < 0.105f) DriveAimed++;
						if (q.alpha == 0 && Math.Abs(MathHelper.WrapAngle(q.rotation - bearing)) < 0.105f) DriveBoth++;
						if (tp.accMedicalBagBoost) DriveBoost++;
					}
					break;
				}
			}
			catch (Exception e) { err += "drive:" + (e.InnerException ?? e).GetType().Name + ":" + (e.InnerException ?? e).Message + ";"; }
			finally { Main.myPlayer = savedMy; }
			if (err != "") DriveErr = err;
			return err;
		}

		/// <summary>Medical Bag state of the stand-in commander: Thorium's pick, ours, the healer-symbol arrow, the boost. <paramref name="aimed"/> = the arrow is visible and points at the levy pick within 6 degrees.</summary>
		public static void MedBag(object cur, out string state, out bool aimed)
		{
			aimed = false;
			Player p = Main.player[0];
			var tp = p.GetModPlayer<ThoriumPlayer>();
			int pick = MedicalBagHook.PickOf(0);
			Projectile sym = null;
			for (int i = 0; i < Main.maxProjectiles; i++) { Projectile q = Main.projectile[i]; if (q.active && q.owner == 0 && q.ModProjectile is ThoriumMod.Projectiles.Healer.HealerSymbol) { sym = q; break; } }
			state = $"acc={tp.accMedicalBag} mirror={tp.headMirror.Active} lowestPlayer={tp.LowestPlayer} levyPick={pick} symbol={(sym != null ? $"alpha{sym.alpha} rot{sym.rotation * 57.2958f:0}" : "none")} boost={tp.accMedicalBagBoost} healBonus={tp.healBonus} DRIVE[ticks{DriveTicks} levyPicked{DrivePick} arrowVisible{DriveVisible} arrowOnTarget{DriveAimed} both{DriveBoth} runBoost{DriveBoost}] driveErr[{DriveErr}]";
			if (pick >= 0)
			{
				NPC n = Main.npc[pick];
				Vector2 from = sym != null ? sym.Center : p.Center;
				float bearing = (n.Center - from).ToRotation();
				state += $" bearing{bearing * 57.2958f:0} pickLife={n.life}/{n.lifeMax}";
				bool alphaOk = false, angleOk = false; int nsym = 0;
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile q = Main.projectile[i];
					if (!q.active || q.owner != 0 || q.ModProjectile is not ThoriumMod.Projectiles.Healer.HealerSymbol) continue;
					nsym++; _symIds.Add(q.identity);
					if (q.alpha == 0) alphaOk = true;
					if (Math.Abs(MathHelper.WrapAngle(q.rotation - (n.Center - q.Center).ToRotation())) < 0.105f) angleOk = true;
				}
				state += $" symbols={nsym} distinctSymbolsSeen={_symIds.Count}";
				_pickTicks++; if (alphaOk) _alphaTicks++; if (angleOk) _angleTicks++;
				state += $" ticks[pick{_pickTicks} alphaVisible{_alphaTicks} angleOk{_angleTicks}] rotDeg={(sym != null ? MathHelper.WrapAngle(sym.rotation - bearing) * 57.2958f : 0):0}";
				if (alphaOk && angleOk) aimed = true;
			}
		}

		// ---- the real PLAYER (baseline) ----
		/// <summary>The stand-in commander has no gear update on a dedicated server: give it a heal bonus directly (every tick, since the game zeroes it).</summary>
		public static void SetHealBonus(Player p, int n) { try { p.GetModPlayer<ThoriumPlayer>().healBonus = n; } catch (Exception) { } }

		public static void PrimePlayerBard(Player p)
		{
			var tp = p.GetModPlayer<ThoriumPlayer>();
			int max = tp.bardResourceMax2 > 0 ? tp.bardResourceMax2 : Math.Max(10, tp.bardResourceMax);
			if (tp.bardResourceMax < 10) tp.bardResourceMax = 10;
			if (tp.bardResourceMax2 < tp.bardResourceMax) tp.bardResourceMax2 = tp.bardResourceMax;
			tp.bardResource = Math.Max(max, tp.bardResourceMax2);
		}
		public static int PlayerBardResource(Player p) { try { return p.GetModPlayer<ThoriumPlayer>().bardResource; } catch (Exception) { return -1; } }
		public static int PlayerBardMax(Player p) { try { return p.GetModPlayer<ThoriumPlayer>().bardResourceMax2; } catch (Exception) { return -1; } }

		private static System.Reflection.FieldInfo _empField;
		/// <summary>Bit n set = the player is under Thorium empowerment type n (read from ThoriumPlayer.Empowerments.ActiveEmpowerments).</summary>
		public static uint PlayerEmpMask(Player p)
		{
			try
			{
				var tp = p.GetModPlayer<ThoriumPlayer>();
				_empField ??= typeof(ThoriumPlayer).GetField("Empowerments", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
				object data = _empField?.GetValue(tp);
				if (data == null) return 0;
				var prop = data.GetType().GetProperty("ActiveEmpowerments") ?? (System.Reflection.MemberInfo)data.GetType().GetField("ActiveEmpowerments") as System.Reflection.PropertyInfo;
				object list = prop != null ? prop.GetValue(data) : data.GetType().GetField("ActiveEmpowerments")?.GetValue(data);
				uint m = 0;
				if (list is System.Collections.IEnumerable en) foreach (object o in en) { int t = Convert.ToInt32(o); if (t >= 0 && t < 32) m |= 1u << t; }
				return m;
			}
			catch (Exception) { return 0; }
		}
	}
}
