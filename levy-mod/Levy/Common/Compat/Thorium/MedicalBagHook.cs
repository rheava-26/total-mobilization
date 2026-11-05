using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.ModLoader;
using ThoriumMod;
using ThoriumMod.Projectiles.Healer;
using Levy.Content.NPCs;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// Thorium's "most wounded ally" (<c>ThoriumPlayer.LowestPlayer</c>, kept by <c>HandleLowestLifeAlly</c>) only ever picks among Players, so the Medical Bag (with its Head Mirror) pointed at nobody once the wounded ones were levies.
	/// What the bag does in Thorium 1.7.2.8: it adds +1 heal bonus, the Head Mirror shows the healer symbol over the wearer's head with an arrow (<c>HealerSymbol</c>) toward <c>LowestPlayer</c>, running toward that ally gives
	/// the wearer a speed boost (<c>PostUpdateEquips</c>), and an ally he heals in combat gets a short life-recovery effect.
	/// This makes the soldiers of the wearer candidates for that pick, by the same measure Thorium uses (the most life lost): three IL/postfix hooks on those routines, no per-item code.
	/// <list type="bullet">
	/// <item><c>HandleLowestLifeAlly</c> postfix: the commander's most wounded soldier replaces the pick when he has lost MORE life than the Player Thorium chose (the wearer himself counts, as in Thorium).</item>
	/// <item><c>HealerSymbol.AI</c> postfix: the arrow points at that soldier.</item>
	/// <item><c>PostUpdateEquips</c> postfix: the Medical Bag's run boost toward him, with Thorium's own numbers.</item>
	/// </list>
	/// Not covered: Inner Flame and Prydwen read the same pick and are unchanged; the bag's life-recovery effect on a healed ally is a Player buff and does not reach soldiers.
	/// </summary>
	[JITWhenModsEnabled("ThoriumMod")]
	public static class MedicalBagHook
	{
		/// <summary>True once the hooks are in place.</summary>
		public static bool Installed { get; private set; }

		private static readonly int[] _pick = new int[Main.maxPlayers + 1];
		private static readonly int[] _lost = new int[Main.maxPlayers + 1];

		/// <summary>The NPC index of the soldier the wearer's arrow points at (-1 = none: an ally Player or the wearer himself is the most wounded).</summary>
		public static int PickOf(int player) => player >= 0 && player < _pick.Length ? _pick[player] : -1;

		public static void Install()
		{
			Installed = false;
			if (System.Environment.GetEnvironmentVariable("LEVY_HARNESS") != null && System.Environment.GetEnvironmentVariable("LEVY_NOALLYHEAL") != null) throw new InvalidOperationException("disabled by LEVY_NOALLYHEAL (A/B test)");
			for (int i = 0; i < _pick.Length; i++) _pick[i] = -1;
			MethodInfo handle = typeof(ThoriumPlayer).GetMethod("HandleLowestLifeAlly", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ?? throw new MissingMethodException("ThoriumPlayer.HandleLowestLifeAlly");
			MethodInfo equips = typeof(ThoriumPlayer).GetMethod("PostUpdateEquips", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null) ?? throw new MissingMethodException("ThoriumPlayer.PostUpdateEquips");
			MethodInfo symbol = typeof(HealerSymbol).GetMethod("AI", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null) ?? throw new MissingMethodException("HealerSymbol.AI");
			if (typeof(ThoriumPlayer).GetProperty("LowestPlayer") == null) throw new MissingMemberException("ThoriumPlayer.LowestPlayer");
			if (typeof(ThoriumPlayer).GetField("accMedicalBag") == null || typeof(ThoriumPlayer).GetField("accMedicalBagBoost") == null) throw new MissingMemberException("ThoriumPlayer.accMedicalBag(Boost)");

			Postfix(handle, nameof(LowestPost));
			MonoModHooks.Add(equips, new Action<Action<ThoriumPlayer>, ThoriumPlayer>((orig, self) => { orig(self); EquipsPost(self); })); // a detour, not an IL edit: the method is ~9000 instructions long and never reaches an appended call
			Postfix(symbol, nameof(SymbolPost));
			Installed = true;
		}

		/// <summary>Append a call to a static method taking the instance (ThoriumPlayer / HealerSymbol) before every return of <paramref name="target"/>.</summary>
		private static void Postfix(MethodInfo target, string hook)
		{
			MethodInfo post = typeof(MedicalBagHook).GetMethod(hook, BindingFlags.Static | BindingFlags.NonPublic) ?? throw new MissingMethodException(hook);
			MonoModHooks.Modify(target, il =>
			{
				var c = new ILCursor(il);
				int rets = 0;
				while (c.TryGotoNext(MoveType.Before, i => i.MatchRet()))
				{
					c.MoveBeforeLabels();
					c.Emit(OpCodes.Ldarg_0);
					c.Emit(OpCodes.Call, post);
					c.GotoNext(MoveType.After, i => i.MatchRet());
					rets++;
				}
				if (rets == 0) throw new InvalidOperationException(target.Name + " has no return to hook");
			});
		}

		// ---- the pick ----

		private static void LowestPost(ThoriumPlayer self)
		{
			try
			{
				Player wearer = self.Player;
				int w = wearer.whoAmI;
				if (w < 0 || w >= _pick.Length) return;
				if (!ThoriumCompatSystem.Active || w != Main.myPlayer) return; // only the commander's own client keeps his pick
				_pick[w] = -1; _lost[w] = 0;
				if (!wearer.active || wearer.dead) return;
				Player cur = self.LowestPlayer >= 0 && self.LowestPlayer < Main.maxPlayers ? Main.player[self.LowestPlayer] : wearer;
				int bestLost = Math.Max(0, cur.statLifeMax2 - cur.statLife);
				int best = -1;
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC n = Main.npc[i];
					if (!n.active || n.life <= 0 || n.life >= n.lifeMax || n.ModNPC is not SoldierNPC s || s.Record == null || !s.IsAlive || !CompanionRequest.Serves(s, wearer)) continue;
					int lost = n.lifeMax - n.life;
					if (lost <= bestLost) continue;
					bestLost = lost; best = i;
				}
				_pick[w] = best; _lost[w] = best >= 0 ? bestLost : 0;
			}
			catch (Exception e) { Warn("choosing the most wounded ally", e); }
		}

		// ---- the arrow ----

		private static void SymbolPost(HealerSymbol self)
		{
			try
			{
				Projectile p = self.Projectile;
				if (p.owner < 0 || p.owner >= Main.maxPlayers) return;
				int idx = _pick[p.owner];
				if (idx < 0) return;
				NPC n = Main.npc[idx];
				Player owner = Main.player[p.owner];
				if (!n.active || n.ModNPC is not SoldierNPC) return;
				if (!owner.GetModPlayer<ThoriumPlayer>().headMirror.Active) return;
				p.alpha = 0;
				p.rotation = (n.Center - p.Center + new Vector2(0f, p.gfxOffY)).ToRotation();
			}
			catch (Exception e) { Warn("pointing the healer symbol", e); }
		}

		// ---- the run boost ----

		private static void EquipsPost(ThoriumPlayer self)
		{
			try
			{
				if (!self.accMedicalBag) return;
				Player pl = self.Player;
				int idx = PickOf(pl.whoAmI);
				if (idx < 0 || !ThoriumCompatSystem.Active) return;
				NPC n = Main.npc[idx];
				if (!n.active || n.ModNPC is not SoldierNPC) return;
				bool toward = (pl.Center.X > n.Center.X && pl.direction == -1) || (pl.Center.X < n.Center.X && pl.direction == 1);
				if (toward)
				{
					pl.moveSpeed += 0.2f; pl.runAcceleration += 0.05f; pl.maxRunSpeed += 1f; pl.accRunSpeed += 1f; // Thorium's own Medical Bag numbers
					self.accMedicalBagBoost = true;
				}
				else self.accMedicalBagBoost = false;
			}
			catch (Exception e) { Warn("the Medical Bag run boost", e); }
		}

		private static bool _warned;
		private static void Warn(string what, Exception e)
		{
			if (_warned) return;
			_warned = true;
			Levy.Instance.Logger.Warn("Levy/Thorium compat: " + what + " threw (reported once): " + e.Message);
		}
	}
}
