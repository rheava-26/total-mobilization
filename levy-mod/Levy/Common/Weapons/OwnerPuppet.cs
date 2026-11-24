using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Compat.Thorium;
using Levy.Common.Globals;
using Levy.Common.Targeting;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Soldier shots are owned by the commander, so any weapon / mod code that reads <c>Main.player[projectile.owner]</c> during a hit (Center, position, direction) would spawn its follow-up effects at the commander.
	/// This puppets the OWNER'S transform to the soldier's for the length of one vanilla call and restores it in a finally, in the same tick:
	///   bit 1: Projectile.Damage (the hit pass: ModProjectile / GlobalProjectile / GlobalNPC OnHit hooks and the strike)
	///   bit 2: Projectile.Kill   (OnKill effects: explosions, split shots)
	///   bit 4: Projectile.Update (every tick of a soldier shot, AI + movement + the hit pass; returning / homing-to-owner projectiles go home to the soldier)
	///   bit 16: a friendly projectile that spawns INSIDE one of these puppeted calls with no soldier tag of its own inherits the soldier's tag
	///   bit 32: LIFE ROUTING. For the length of the pass the owner's life and max life ARE the soldier's (so "below half health", "already full" and the clamp at max all read the soldier's), and whatever the pass
	///          raised them by - lifesteal: vanilla Vampire Knives' VampireHeal bolt, Thorium HealLife on its weapon hit, Player.Heal, a bare statLife += - is the soldier's own gain: the commander's real life is put back
	///          untouched and the soldier is given the gain (with the floating "+N" on him). The owner's lifesteal budget (Player.lifeSteal) is the soldier's own for the pass (<see cref="LifeStealPool"/>) and the commander's is put back in the finally. Heals meant for the commander (a soldier's ally heal) run inside <see cref="BeginAllow"/>, which puts his real life and place back.
	///   bit 64: DRAW. Main.DrawProjDirect of a soldier shot whose draw code reads its owner's place (a flail's chain ends at <c>Main.GetPlayerArmPosition(projectile)</c>, a held gun is drawn from the owner's arm) runs with the owner's
	///          transform on the soldier (<see cref="BeginDraw"/>), so the chain runs to the levy and not across the screen to the commander. Only shots whose own draw code reads the owner (<see cref="OwnerLockProbe.ReadsOwnerAtDraw"/>) are moved.
	///   bit 128: USE BUTTONS. A soldier's modded shot whose own AI reads its owner's use buttons (<c>Player.channel</c> / <c>controlUseItem</c>, Calamity's <c>CantUseHoldout</c>: a gun holdout that charges while the button is down, a mace flail that spins
	///          while it is held and is thrown when it is let go) reads the COMMANDER's buttons, which are down whenever the commander is firing her own weapon: the levy's holdout then charges for as long as she holds hers and its
	///          shots come out when she lets go, and its flail spins on the spot and never throws. For the length of that shot's update the owner's buttons read as a soldier's shot sees them with a commander who is not holding anything
	///          (the button up, the use released), which is what the weapons were measured with (<see cref="ModShotKinds"/>), and are put back in the same finally. Only shots whose own AI reads the buttons are touched (<see cref="OwnerLockProbe.ReadsUseButtons"/>).
	///   bit 256: LIVE OWNER. While the commander is dead (the mind-control death link drives a levy then) every shot of that levy is still owned by her, and a great many modded projectiles end themselves at once when their owner is
	///          dead (Calamity's holdouts and summons: <c>if (Owner.dead) Kill()</c>): Tumbleweed, Ice Barrage and Undine's Retribution made nothing or hit nothing. For the length of each puppeted pass of a soldier's shot the owner reads as alive, and is put back dead in the same finally.
	///   bit 8: Thorium's cached "local ThoriumPlayer" (PlayerHelper.localThoriumPlayer) is pointed at the soldier dummy's ThoriumPlayer around the Shoot hooks in WeaponUse.TryRealShoot and inside puppeted Damage / Kill
	///          (GetThoriumPlayer returns that cache for any player whose whoAmI == Main.myPlayer, which the dummy is given for the call).
	/// Only the transform is moved: the Player object, its ModPlayers, buffs and life stay the commander's, so loot credit and boss-kill resolution still run under the real player.
	/// On by default through <c>LevyConfig.SoldierEffectsAtSoldier</c> (all six bits). The harness env <c>LEVY_ONHIT_FIX</c> (a bit mask) wins when set, so 0 measures the unfixed behaviour (31 = everything but life routing).
	/// Safety: every swap restores in a finally. Calls nest (a child projectile killed or updated inside a puppeted pass) through a saved-state stack, so each exit restores exactly what its own entry saw.
	/// The owner must be a real active player and the body a live soldier. Player-state net messages (4, 13) sent while a swap is open go out with the real transform.
	/// </summary>
	public class OwnerPuppet : ModSystem
	{
		/// <summary>Effective bit mask, read on the hot path. Written only by <see cref="Refresh"/>.</summary>
		internal static int Mask = 511;
		private static bool _envSet; private static int _envMask; private static bool _cfgOn = true; private static bool _hooked;
		internal static int UnpuppetedOwner, UnpuppetedBody; // soldier shots seen with no active owner / no live body (harness diagnostics)
		internal static int LiveOwnerPasses; internal static int Swaps, Restores, ThSwaps, Inherited, NetGuards, MaxDepth; // counters for the harness
		/// <summary>The soldier whose projectile is inside a puppeted call right now (null otherwise).</summary>
		internal static SoldierNPC Context;
		/// <summary>True when bit 16 is on and a puppeted call is running: an untagged friendly projectile spawned now belongs to that soldier.</summary>
		internal static bool TryInherit(Projectile p, out SoldierNPC body) { body = Context; return (Mask & 16) != 0 && body != null && !p.hostile && p.owner < Main.maxPlayers; }

		/// <summary>Called by the config on load / change with the cached server-side switch.</summary>
		internal static void SetConfig(bool on) { _cfgOn = on; Refresh(); }
		private static void Refresh() => Mask = _envSet ? _envMask : (_cfgOn ? 511 : 0);

		public override void Load()
		{
			string v = Environment.GetEnvironmentVariable("LEVY_ONHIT_FIX");
			_envSet = !string.IsNullOrEmpty(v) && int.TryParse(v, out _envMask);
			Refresh();
			if (_envSet && _envMask == 0) return; // the harness asked for the unfixed behaviour: no detours at all
			On_Projectile.Damage += DamageDetour;
			On_Projectile.Kill += KillDetour;
			On_Projectile.Update += UpdateDetour;
			if (!Main.dedServ) On_Main.DrawProjDirect += DrawDirectDetour;
			On_NetMessage.SendData += SendDataDetour;
			On_Player.HealEffect += HealEffectDetour;
			_hooked = true;
			Mod.Logger.Info("OWNERPUPPET: enabled mask=" + Mask + (_envSet ? " (env override)" : " (config)"));
		}

		public override void Unload()
		{
			if (_hooked) { On_Projectile.Damage -= DamageDetour; On_Projectile.Kill -= KillDetour; On_Projectile.Update -= UpdateDetour; if (!Main.dedServ) On_Main.DrawProjDirect -= DrawDirectDetour; On_NetMessage.SendData -= SendDataDetour; On_Player.HealEffect -= HealEffectDetour; }
			_hooked = false; _envSet = false; _cfgOn = true; Mask = 511; _depth = 0; Context = null; _curTp = null; LifeStealPool.Unload();
		}

		private static bool Puppetable(Projectile p, out Player owner, out SoldierNPC body)
		{
			owner = null; body = null;
			if (p.owner < 0 || p.owner >= Main.maxPlayers || !p.TryGetGlobalProjectile(out SoldierProjectileTag t) || !t.IsSoldierShot) return false;
			owner = Main.player[p.owner];
			if (owner == null || !owner.active || owner.whoAmI != p.owner) { UnpuppetedOwner++; return false; } // a real, active player only
			body = EnemyTargeting.LiveBody(t.SoldierIndex);
			bool ok = body != null && body.NPC.active && !ReferenceEquals(body.Dummy, owner);
			if (!ok) UnpuppetedBody++;
			return ok;
		}

		/// <summary>One open swap: what the owner's transform and the Thorium cache were when this call entered.</summary>
		private struct Frame { public bool Moved; public Player P; public Vector2 Pos, Vel, Old; public int Dir; public object Th; public SoldierNPC PrevCtx; public bool Route; public int Life, LifeMax, SolLife, Allow; public SoldierNPC Body; public float Pool, MyPool; public Player MyP; public SoldierNPC Outer; public bool WasDead, WasGhost;
			/// <summary>The cursor puppet (CombatFix.Cursor): the commander's cursor and Calamity's per-player cursor offset as this pass found them.</summary>
			public bool Cur; public Vector2 CurScreen, CurDelta; public int CurMx, CurMy; public float CurRot; public object CurCal;
			/// <summary>The use-button puppet: the commander's buttons as this pass found them.</summary>
			public bool Btn, BCh, BUse, BTile, BRel;
			/// <summary>The projectile and the pass (bit 1, 2 or 4) this frame was opened for.</summary>
			public Projectile Proj; public int Bit;
			/// <summary>The levy's weapon counters were opened on the owner by this frame (<see cref="PlayerCounters"/>).</summary>
			public bool Ctr;
			/// <summary>The owner's ownedProjectileCounts were swapped for the levy's own by this frame.</summary>
			public bool Cnt; }

		private const int MaxFrames = 32;
		private static readonly Frame[] _stack = new Frame[MaxFrames];
		private static int _depth;
		internal static int DebugDepth => _depth;

		private static bool Enter(Projectile self, int bit)
		{
			// bit 8 (Thorium cache) rides along with Damage / Kill: GetThoriumPlayer(Main.player[owner]) would otherwise resolve to the commander's ThoriumPlayer for a soldier's hit
			int m = Mask;
			bool moveTransform = (m & bit) != 0, swapCache = (m & 8) != 0 && bit != 4, inherit = (m & 16) != 0, route = (m & 32) != 0 && self.type != ProjectileID.SpiritHeal; // a Spectre-armor spirit bolt goes to the most wounded ALLY (an ally heal, not the wearer's lifesteal): left as it was
			if (!(moveTransform || swapCache || inherit || route) || _depth >= MaxFrames || !Puppetable(self, out Player p, out SoldierNPC body)) return false;
			ref Frame f = ref _stack[_depth];
			f.Moved = moveTransform; f.P = p; f.Pos = p.position; f.Vel = p.velocity; f.Old = p.oldPosition; f.Dir = p.direction; f.Proj = self; f.Bit = bit;
			f.PrevCtx = Context; f.Th = swapCache ? SwapThorium(body.Dummy) : null;
			f.Body = body; f.Route = route; f.Allow = 0; f.WasDead = false; f.WasGhost = false;
			if ((m & 256) != 0 && (p.dead || p.ghost)) { f.WasDead = p.dead; f.WasGhost = p.ghost; p.dead = false; p.ghost = false; LiveOwnerPasses++; }
			if (route)
			{
				f.Life = p.statLife; f.LifeMax = p.statLifeMax2; // as this pass found him (the commander's own, or an enclosing pass's soldier)
				f.SolLife = Math.Max(1, body.NPC.life);
				p.statLifeMax2 = Math.Max(1, body.NPC.lifeMax); p.statLife = Math.Min(f.SolLife, p.statLifeMax2);
				RouteOpens++;
				OpenPool(ref f, p, body);
			}
			f.Ctr = PlayerCounters.Open(p, body); // the levy's own weapon counters on the owner's player for the pass (a shark gun's hit count)
			f.Cnt = OpenCounts(self, p, body); // a shot that counts its owner's projectiles counts the levy's (Seadragon's fish die when no holdout of their owner's is out)
			_depth++; if (_depth > MaxDepth) MaxDepth = _depth;
			Context = body;
			// the commander's use buttons read as a levy's shot has always expected them (up) for a modded shot whose own AI reads them (see bit 128)
			if (bit == 4 && (m & 128) != 0 && self.ModProjectile != null && OwnerLockProbe.ReadsUseButtons(self.type) && (p.channel || p.controlUseItem || p.controlUseTile || !p.releaseUseItem))
			{
				f.Btn = true; f.BCh = p.channel; f.BUse = p.controlUseItem; f.BTile = p.controlUseTile; f.BRel = p.releaseUseItem;
				p.channel = false; p.controlUseItem = false; p.controlUseTile = false; p.releaseUseItem = true;
				ButtonPuppets++;
			}
			if (moveTransform) { Swaps++; p.Center = body.NPC.Center; p.oldPosition = p.position; p.velocity = body.NPC.velocity; p.direction = body.NPC.direction == 0 ? 1 : body.NPC.direction; }
			// the commander's CURSOR is the soldier's target for any hook of a shot whose own code reads a cursor (its AI, its hit and kill hooks): without it the shot reads wherever the commander's mouse is
			if (CombatFix.On(CombatFix.Cursor) && self.ModProjectile != null && !ModShotKinds.Off && OwnerLockProbe.ReadsCursor(self.type))
			{
				Vector2 at = body.AimCursor();
				f.Cur = true; f.CurScreen = Main.screenPosition; f.CurMx = Main.mouseX; f.CurMy = Main.mouseY;
				global::Levy.Common.Owner.AimView.Set(at, body.NPC.Center);
				f.CurCal = AltPolicies.CalamityPlayerOf(p);
				if (f.CurCal != null) { (f.CurDelta, f.CurRot) = AltPolicies.GetCalAimPublic(f.CurCal); AltPolicies.SetCalAimPublic(f.CurCal, at - p.MountedCenter); }
				SoldierNPC.CursorPuppets++;
			}
			return true;
		}

		private static void Exit()
		{
			if (_depth <= 0) return;
			ref Frame f = ref _stack[--_depth];
			try
			{
				if (f.Cur)
				{
					Main.screenPosition = f.CurScreen; Main.mouseX = f.CurMx; Main.mouseY = f.CurMy;
					if (f.CurCal != null) AltPolicies.SetCalAimPublic(f.CurCal, f.CurDelta, f.CurRot);
				}
				if (f.Btn) { f.P.channel = f.BCh; f.P.controlUseItem = f.BUse; f.P.controlUseTile = f.BTile; f.P.releaseUseItem = f.BRel; }
				if (f.Moved) { f.P.position = f.Pos; f.P.velocity = f.Vel; f.P.oldPosition = f.Old; f.P.direction = f.Dir; Restores++; }
				if (f.Route) CloseRoute(ref f);
			}
			finally { if (f.Cnt) CloseCounts(); if (f.Ctr) PlayerCounters.Close(); if (f.WasDead) f.P.dead = true; if (f.WasGhost) f.P.ghost = true; if (f.Route) ClosePool(ref f); RestoreThorium(f.Th); Context = f.PrevCtx; f = default; }
		}


		private static Player _cntOwner, _cntDummy;
		/// <summary>Lab counter: passes run with the levy's own projectile counts on the owner.</summary>
		internal static long CountSwaps;
		/// <summary>Lab A/B switch (env LEVY_OWNEDCOUNTS=0): a levy's shot reads the commander's ownedProjectileCounts, as before the gunparity pass.</summary>
		internal static readonly bool OwnedCountsOff = Environment.GetEnvironmentVariable("LEVY_OWNEDCOUNTS") == "0";

		/// <summary>
		/// For the pass of a levy's shot whose own code reads its owner's <c>ownedProjectileCounts</c> (<see cref="OwnerLockProbe.ReadsOwnedCounts"/>), the owner's count array IS the levy's (his dummy's, counted from the projectiles tagged to him
		/// this tick): the two array references are swapped and swapped back in the same finally. Nested passes find it open and leave it.
		/// </summary>
		private static bool OpenCounts(Projectile self, Player p, SoldierNPC body)
		{
			if (OwnedCountsOff || _cntOwner != null || self.ModProjectile == null || body?.Dummy == null || ReferenceEquals(body.Dummy, p) || !OwnerLockProbe.ReadsOwnedCounts(self.type)) return false;
			Player d = body.Dummy;
			HeldWeapons.CountOwned(body);
			int[] mine = p.ownedProjectileCounts; p.ownedProjectileCounts = d.ownedProjectileCounts; d.ownedProjectileCounts = mine;
			_cntOwner = p; _cntDummy = d; CountSwaps++;
			return true;
		}

		private static void CloseCounts()
		{
			Player p = _cntOwner, d = _cntDummy; _cntOwner = null; _cntDummy = null;
			if (p == null || d == null) return;
			int[] levy = p.ownedProjectileCounts; p.ownedProjectileCounts = d.ownedProjectileCounts; d.ownedProjectileCounts = levy;
		}

		/// <summary>An open body-lend (see <see cref="LendTransform"/>); Dispose puts the commander's transform back.</summary>
		internal readonly struct Lend : IDisposable
		{
			private readonly bool _open;
			internal Lend(bool open) { _open = open; }
			public void Dispose() { if (_open) Exit(); }
		}

		/// <summary>Frames the body-lend opened (lab counter).</summary>
		internal static int BodyLends;
		/// <summary>Lab A/B switch: LEVY_FACINGFIX=0 runs the levy's use windows as they ran before the facing fix (no body-lend around spawns, swings and a beam's hit pass; a chain projectile's hooks outside its AI body and its Kill see the empty player of slot 255). The facing world check measures both.</summary>
		internal static readonly bool FacingFixOn = Environment.GetEnvironmentVariable("LEVY_FACINGFIX") != "0";

		/// <summary>
		/// THE LEVY'S USE WINDOW. Shots that fly are puppeted every tick by <see cref="Enter"/> (Update / Damage / Kill), but the moments a levy MAKES something are not shots yet: the spawn hooks of a shot the weapon's
		/// Shoot hook makes (OnSpawn, SetDefaults, a ModProjectile that copies <c>Main.player[owner].direction</c> into its own), the hit of a melee swing (the item's hit hooks and every global that looks at the owner), the damage
		/// pass of a held weapon's beam. All of them run while the shot's owner is the commander's slot (<see cref="WeaponUse.OwnerFor"/>), so each read "the owner" and found the COMMANDER: her facing, her place,
		/// her velocity. With her facing the other way than the levy's, a lash, a swing or a beam came out of the levy mirrored, and an army split in two halves facing opposite ways fought with two different weapons.
		/// This is the same transform lend as the shot's own puppet (place, velocity and facing of the soldier, nothing else: her life, buffs, ModPlayers and use state stay hers, and the use guard still puts the rest back),
		/// opened around the levy's own use. <c>using (OwnerPuppet.LendTransform(body)) { ... }</c>; calls nest; it opens nothing when the owner is not a real active player, so a dedicated server's null owner is left alone.
		/// </summary>
		internal static Lend LendTransform(SoldierNPC body)
		{
			if (!FacingFixOn || (Mask & 4) == 0 || body == null || body.NPC == null || !body.NPC.active || _depth >= MaxFrames) return default;
			int o = SoldierMinions.TrueMyPlayer;
			if (o < 0 || o >= Main.maxPlayers) return default;
			Player p = Main.player[o];
			if (p == null || !p.active || p.whoAmI != o || ReferenceEquals(body.Dummy, p) || global::Levy.Common.Owner.SoldierOwner.IsDummy(p)) return default;
			ref Frame f = ref _stack[_depth];
			f = default;
			f.Moved = true; f.P = p; f.Pos = p.position; f.Vel = p.velocity; f.Old = p.oldPosition; f.Dir = p.direction; f.Body = body; f.PrevCtx = Context;
			f.Ctr = PlayerCounters.Open(p, body);
			_depth++; if (_depth > MaxDepth) MaxDepth = _depth;
			BodyLends++; Swaps++;
			p.Center = body.NPC.Center; p.oldPosition = p.position; p.velocity = body.NPC.velocity; p.direction = body.NPC.direction == 0 ? 1 : body.NPC.direction;
			return new Lend(true);
		}

		// ---- bit 32: life routing ----
		internal static int RouteOpens, RoutedHeals, RoutedLife, HealTextOpenUnrouted, HealTextOutside;

		// ---- bit 32: the dealing levy's own lifesteal budget ----
		internal static int PoolOpens;

		/// <summary>
		/// For the pass the owner's <c>lifeSteal</c> IS the levy's own budget (<see cref="LifeStealPool"/>). Vanilla's spend reads <c>Main.player[Main.myPlayer]</c>, Calamity's the owner: both fields hold the levy's value.
		/// Passes nest (Update -> Damage -> a child's Kill): a pass inside another pass of the SAME levy keeps the field as it is (the enclosing pass writes it back); inside another levy's pass it first files that
		/// levy's live value, swaps in its own and, on the way out, puts the enclosing levy's current value back.
		/// </summary>
		private static void OpenPool(ref Frame f, Player owner, SoldierNPC body)
		{
			f.MyP = null; f.Pool = float.NaN; f.Outer = null;
			if (LifeStealPool.SharedForTest) return;
			SoldierNPC outer = null;
			for (int k = _depth - 1; k >= 0; k--)
				if (_stack[k].Route && ReferenceEquals(_stack[k].P, owner) && !float.IsNaN(_stack[k].Pool)) { outer = _stack[k].Body; break; }
			if (outer != null && ReferenceEquals(outer, body)) return; // the enclosing pass of this very levy holds his budget in the field
			if (outer != null) LifeStealPool.Store(outer, owner.lifeSteal);
			float pool = LifeStealPool.Current(body);
			f.Outer = outer; f.Pool = owner.lifeSteal; owner.lifeSteal = pool;
			if (Main.myPlayer >= 0 && Main.myPlayer < Main.maxPlayers)
			{
				Player my = Main.player[Main.myPlayer];
				if (my != null && !ReferenceEquals(my, owner)) { f.MyP = my; f.MyPool = my.lifeSteal; my.lifeSteal = pool; }
			}
			PoolOpens++;
		}

		/// <summary>What the pass spent comes off the levy's budget; the value that was in the field before (the commander's own, or the enclosing levy's current one) is put back.</summary>
		private static void ClosePool(ref Frame f)
		{
			if (float.IsNaN(f.Pool)) return; // this pass kept the field as it found it (inside the same levy's pass, or the test seam)
			Player owner = f.P;
			float left = owner.lifeSteal;
			if (f.MyP != null) left = Math.Min(left, f.MyP.lifeSteal);
			LifeStealPool.Store(f.Body, left);
			float back = f.Outer != null ? f.Outer.LifeStealPool : f.Pool;
			owner.lifeSteal = back;
			if (f.MyP != null) { f.MyP.lifeSteal = f.Outer != null ? back : f.MyPool; f.MyP = null; }
			f.Outer = null;
		}

		/// <summary>The pass is over: what the owner's life gained while the transform sat on the soldier is the soldier's gain. Put the commander's life back and give the soldier what he earned.</summary>
		private static void CloseRoute(ref Frame f)
		{
			Player p = f.P;
			int gained = p.statLife - Math.Min(f.SolLife, Math.Max(1, f.Body.NPC.lifeMax));
			p.statLife = f.Life; p.statLifeMax2 = f.LifeMax;
			if (gained <= 0) return;
			RoutedHeals++; RoutedLife += gained;
			GiveToSoldier(f.Body, gained);
		}

		/// <summary>Life the soldier earned from his own hit (lifesteal): his own life, with the floating number on him. On a multiplayer client the server owns his life, so the same request an ally heal uses.</summary>
		private static void GiveToSoldier(SoldierNPC body, int amount)
		{
			NPC n = body?.NPC;
			if (n == null || !n.active || n.life <= 0 || n.life >= n.lifeMax || amount <= 0) return;
			if (Main.netMode == NetmodeID.MultiplayerClient) CompanionRequest.SendHeal(new System.Collections.Generic.List<(int npc, int amount)> { (n.whoAmI, amount) });
			else SoldierHeal.Restore(n, amount);
		}

		/// <summary>True while a puppeted pass for <paramref name="p"/> is open with life routing, and no sanctioned commander heal is running.</summary>
		private static bool Routing(Player p)
		{
			for (int k = _depth - 1; k >= 0; k--) if (_stack[k].Route && ReferenceEquals(_stack[k].P, p)) return _stack[k].Allow == 0;
			return false;
		}

		/// <summary>The commander's REAL position (the transform is on the soldier during a puppeted pass).</summary>
		internal static Vector2 RealCenter(Player p)
		{
			for (int k = 0; k < _depth; k++) if (_stack[k].Moved && ReferenceEquals(_stack[k].P, p)) return _stack[k].Pos + new Vector2(p.width * 0.5f, p.height * 0.5f);
			return p.Center;
		}

		/// <summary>The commander's REAL life and max life (during a routed pass the owner's life fields hold the soldier's).</summary>
		internal static void RealLife(Player p, out int life, out int max)
		{
			for (int k = 0; k < _depth; k++) if (_stack[k].Route && ReferenceEquals(_stack[k].P, p)) { life = _stack[k].Life; max = _stack[k].LifeMax; return; }
			life = p.statLife; max = p.statLifeMax2;
		}

		/// <summary>True when a puppeted pass is open for <paramref name="p"/>.</summary>
		internal static bool IsPuppeted(Player p)
		{
			for (int k = 0; k < _depth; k++) if (ReferenceEquals(_stack[k].P, p)) return true;
			return false;
		}

		/// <summary>The soldier whose pass is open for <paramref name="p"/> (outermost), or null.</summary>
		internal static SoldierNPC CasterOf(Player p)
		{
			for (int k = 0; k < _depth; k++) if (ReferenceEquals(_stack[k].P, p)) return _stack[k].Body;
			return null;
		}

		/// <summary>
		/// A sanctioned heal on the COMMANDER inside a puppeted pass (an ally heal a soldier cast, which is not lifesteal): the commander is put back where he really stands with his real life, so the heal lands on him,
		/// is not taken for the soldier's lifesteal, and its floating number shows over him. Dispose puts the soldier's vitals and place back.
		/// </summary>
		internal static AllowScope BeginAllow(Player p)
		{
			int top = -1, first = -1, firstMoved = -1;
			for (int k = 0; k < _depth; k++)
				if (ReferenceEquals(_stack[k].P, p)) { top = k; if (first < 0 && _stack[k].Route) first = k; if (firstMoved < 0 && _stack[k].Moved) firstMoved = k; }
			if (top < 0) return default;
			var sc = new AllowScope { Idx = top, First = first, P = p, SaveLife = p.statLife, SaveMax = p.statLifeMax2, Pos = p.position, Active = true };
			_stack[top].Allow++;
			if (first >= 0) { p.statLifeMax2 = _stack[first].LifeMax; p.statLife = _stack[first].Life; }
			if (firstMoved >= 0) p.position = _stack[firstMoved].Pos;
			return sc;
		}

		internal struct AllowScope : IDisposable
		{
			internal int Idx, First, SaveLife, SaveMax; internal Player P; internal bool Active; internal Vector2 Pos;
			public void Dispose()
			{
				if (!Active) return;
				Active = false;
				if (Idx >= _depth || !ReferenceEquals(_stack[Idx].P, P)) return;
				if (_stack[Idx].Allow > 0) _stack[Idx].Allow--;
				P.position = Pos;
				if (First >= 0 && First < _depth && ReferenceEquals(_stack[First].P, P))
				{
					_stack[First].Life = P.statLife; // whatever the heal did to his real life is his life now
					P.statLife = SaveLife; P.statLifeMax2 = SaveMax;
				}
			}
		}

		private static void HealEffectDetour(On_Player.orig_HealEffect orig, Player self, int healAmount, bool broadcast)
		{ _hl_HealEffectDetour_3++;
			if (_depth > 0 && (Mask & 32) != 0 && Routing(self)) return; // the number is shown on the soldier who earns it (GiveToSoldier)
			if (self.whoAmI == Main.myPlayer) { if (_depth > 0) HealTextOpenUnrouted++; else HealTextOutside++; } // diagnostics for the harness
			orig(self, healAmount, broadcast);
		} private static int _hl_HealEffectDetour_3;

		/// <summary>
		/// THE NESTED HIT PASS. Projectile.Update runs the shot's hit pass (Projectile.Damage) inside itself, about twice a shot update; the update pass has already put the owner on the soldier, so a second swap for the hit pass
		/// would write the same transform, the same life and lifesteal budget and the same live owner again and put them back at its end. It is skipped when the open frame is this very shot's update pass and everything the hit pass's
		/// entry would write is still in place (the transform, the soldier's max life, the owner alive, and, for a shot that reads the cursor, the commander's cursor and Calamity's aim already on the levy's target). What changes: a lifesteal gain made by the hit stays in the owner's life (the soldier's, for the pass)
		/// until the update pass closes, and the soldier is given it then, in the same tick, instead of right after the hit; the totals are the same (both paths add the gain with the same cap). Thorium's local-player cache,
		/// which only the hit pass swaps, is still swapped around the call. LEVY_NESTSKIP=0 turns it off (lab A/B, the parity check).
		/// </summary>
		internal static bool NestSkipOn = Environment.GetEnvironmentVariable("LEVY_NESTSKIP") != "0";
		internal static int NestSkips, NestFull;

		/// <summary>Lab (LEVY_SWAPPROF=1): why a hit pass inside an open frame was not skipped (0 another pass or projectile, 1 cursor puppet, 2 mask, 3 owner, 4 body, 5 transform moved, 6 max life).</summary>
		internal static readonly long[] NestWhy = new long[7];
		private static bool No(int why) { if (SwapProf.On) NestWhy[why]++; return false; }

		private static bool CanSkipNested(Projectile self)
		{
			if (!NestSkipOn || _depth <= 0) return false;
			ref Frame f = ref _stack[_depth - 1];
			if (f.Bit != 4 || !ReferenceEquals(f.Proj, self) || f.Body == null) return No(0);
			int m = Mask;
			if (((m & 1) != 0) != ((m & 4) != 0)) return No(2); // the hit pass would move the transform where the update pass did not, or the other way round
			Player p = f.P;
			if (p == null || !p.active || p.dead || p.ghost || self.owner != p.whoAmI || !ReferenceEquals(Main.player[self.owner], p)) return No(3);
			if (!self.TryGetGlobalProjectile(out SoldierProjectileTag t) || !ReferenceEquals(EnemyTargeting.LiveBody(t.SoldierIndex), f.Body) || ReferenceEquals(f.Body.Dummy, p)) return No(4);
			NPC b = f.Body.NPC;
			if (f.Moved)
			{
				Vector2 pos = p.position; p.Center = b.Center; bool same = p.position == pos; p.position = pos; // the hit pass's own write, compared and undone
				if (!same || p.velocity != b.velocity || p.direction != (b.direction == 0 ? 1 : b.direction) || p.oldPosition != p.position) return No(5);
			}
			if (f.Route && p.statLifeMax2 != Math.Max(1, b.lifeMax)) return No(6);
			if (f.Cur)
			{
				// the cursor puppet: the hit pass's entry would aim the commander's cursor (and Calamity's per-player aim) at the levy's target again; skipped only when they already read exactly that. Its one call to the
				// levy's AimCursor is made here instead (the same call the entry makes: it holds the target's place for the fallback)
				Vector2 at = f.Body.AimCursor(), sp = global::Levy.Common.Owner.AimView.ScreenFor(b.Center);
				if (Main.screenPosition != sp || Main.mouseX != (int)(at.X - sp.X) || Main.mouseY != (int)(at.Y - sp.Y)) return No(1);
				if (f.CurCal != null)
				{
					Vector2 want = at - p.MountedCenter; var (d, r) = AltPolicies.GetCalAimPublic(f.CurCal);
					if (d != want || r != want.ToRotation()) return No(1);
				}
				SoldierNPC.CursorPuppets++;
			}
			return true;
		}

		private static void DamageDetour(On_Projectile.orig_Damage orig, Projectile self)
		{ _hl_DamageDetour_1++;
			long s0 = SwapProf.On ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
			if (Mask != 0 && CanSkipNested(self))
			{
				NestSkips++;
				object th = (Mask & 8) != 0 ? SwapThorium(_stack[_depth - 1].Body.Dummy) : null; // only the hit pass swaps Thorium's cache: kept
				long s1 = SwapProf.On ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
				try { orig(self); } finally { long s2 = SwapProf.On ? System.Diagnostics.Stopwatch.GetTimestamp() : 0; RestoreThorium(th); if (SwapProf.On) { SwapProf.SkipSwap += (s1 - s0) + (System.Diagnostics.Stopwatch.GetTimestamp() - s2); SwapProf.SkipN++; } }
				return;
			}
			if (SwapProf.On) SwapProf.SkipSwap += System.Diagnostics.Stopwatch.GetTimestamp() - s0; // the test that did not skip is part of the hit pass's swap cost too
			if (_depth > 0) NestFull++;
			if (SwapProf.On) { DamageTimed(orig, self); return; }
			if (Mask == 0 || !Enter(self, 1)) { orig(self); return; }
			try { orig(self); } finally { Exit(); }
		} private static int _hl_DamageDetour_1;
		private static void KillDetour(On_Projectile.orig_Kill orig, Projectile self)
		{ _hl_KillDetour_4++;
			if (Mask == 0 || !Enter(self, 2)) { orig(self); return; }
			try { orig(self); } finally { Exit(); }
		} private static int _hl_KillDetour_4;
		private static void UpdateDetour(On_Projectile.orig_Update orig, Projectile self, int i)
		{ _hl_UpdateDetour_6++;
			if (SwapProf.On) { UpdateTimed(orig, self, i); return; }
			if (Mask != 0 && global::Levy.Common.Perf.PerfAnalyzer.SampleArmy) { UpdateSampled(orig, self, i); return; }
			if (Mask == 0 || !Enter(self, 4)) { orig(self, i); return; }
			try { orig(self, i); } finally { Exit(); }
		} private static int _hl_UpdateDetour_6;

		/// <summary>The performance analyzer's sampled update (one in eleven): the swap around a soldier shot's update is timed and charged to the levy's weapon. Otherwise the same as the plain path.</summary>
		private static void UpdateSampled(On_Projectile.orig_Update orig, Projectile self, int i)
		{
			long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
			if (!Enter(self, 4)) { orig(self, i); return; }
			long t1 = System.Diagnostics.Stopwatch.GetTimestamp(); int soldier = Context?.NPC.whoAmI ?? -1;
			try { orig(self, i); }
			finally { long t2 = System.Diagnostics.Stopwatch.GetTimestamp(); Exit(); global::Levy.Common.Perf.PerfAnalyzer.NoteSwap(soldier, (t1 - t0) + (System.Diagnostics.Stopwatch.GetTimestamp() - t2)); }
		}

		// ---- lab (LEVY_SWAPPROF=1): the swap's own cost against the whole update of a soldier's shot
		private static void UpdateTimed(On_Projectile.orig_Update orig, Projectile self, int i)
		{
			long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
			bool entered = Mask != 0 && Enter(self, 4);
			long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
			if (!entered) { SwapProf.EnterMiss += t1 - t0; orig(self, i); return; }
			long t2 = 0;
			try { orig(self, i); }
			finally { t2 = System.Diagnostics.Stopwatch.GetTimestamp(); Exit(); long t3 = System.Diagnostics.Stopwatch.GetTimestamp(); SwapProf.UpdSwap += (t1 - t0) + (t3 - t2); SwapProf.UpdTotal += t3 - t0; SwapProf.UpdN++; }
		}
		private static void DamageTimed(On_Projectile.orig_Damage orig, Projectile self)
		{
			long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
			bool entered = Mask != 0 && Enter(self, 1);
			long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
			if (!entered) { orig(self); return; }
			long t2 = 0;
			try { orig(self); }
			finally { t2 = System.Diagnostics.Stopwatch.GetTimestamp(); Exit(); long t3 = System.Diagnostics.Stopwatch.GetTimestamp(); SwapProf.DamSwap += (t1 - t0) + (t3 - t2); SwapProf.DamN++; }
		}

		// ---- bit 64: draw ----
		/// <summary>One open draw puppet (a draw never nests inside an update pass, and one projectile draws at a time).</summary>
		private static bool _drawOpen; private static Player _drawP; private static Vector2 _drawPos, _drawVel, _drawOld; private static int _drawDir;
		internal static int DrawPuppets, ButtonPuppets;

		/// <summary>
		/// Moves the owner's transform onto the soldier for the length of one projectile's draw, when the shot is a soldier's and its own draw code reads its owner. Returns false when nothing was moved (no call to <see cref="EndDraw"/> then).
		/// <paramref name="force"/> is the harness (a dedicated server never draws): it works whatever the mask says.
		/// </summary>
		internal static bool BeginDraw(Projectile p, bool force = false)
		{
			if (_drawOpen || ((Mask & 64) == 0 && !force) || p == null || !p.active || p.ModProjectile == null || p.owner < 0 || p.owner >= Main.maxPlayers) return false;
			if (!OwnerLockProbe.ReadsOwnerAtDraw(p.type) || !Puppetable(p, out Player owner, out SoldierNPC body)) return false;
			_drawOpen = true; _drawP = owner; _drawPos = owner.position; _drawVel = owner.velocity; _drawOld = owner.oldPosition; _drawDir = owner.direction;
			owner.Center = body.NPC.Center; owner.oldPosition = owner.position; owner.velocity = body.NPC.velocity; owner.direction = body.NPC.direction == 0 ? 1 : body.NPC.direction;
			DrawPuppets++;
			return true;
		}

		internal static void EndDraw()
		{
			if (!_drawOpen) return;
			_drawOpen = false;
			_drawP.position = _drawPos; _drawP.velocity = _drawVel; _drawP.oldPosition = _drawOld; _drawP.direction = _drawDir; _drawP = null;
		}

		private static void DrawDirectDetour(On_Main.orig_DrawProjDirect orig, Main self, Projectile proj)
		{ _hl_DrawDirectDetour_2++;
			bool moved = false;
			try { moved = BeginDraw(proj); } catch (Exception) { moved = false; EndDraw(); }
			try { orig(self, proj); } finally { if (moved) EndDraw(); }
		} private static int _hl_DrawDirectDetour_2;

		/// <summary>Player-state messages (4 SyncPlayer, 13 PlayerControls) carry the player's position. If one is sent while that player's transform is puppeted (a hook calling NetMessage.SendData), send it with the real transform.</summary>
		private static void SendDataDetour(On_NetMessage.orig_SendData orig, int msgType, int remoteClient, int ignoreClient, Terraria.Localization.NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)
		{ _hl_SendDataDetour_5++;
			if (_depth > 0 && (msgType == MessageID.PlayerHeal || msgType == MessageID.SpiritHeal) && number >= 0 && number < Main.maxPlayers && Routing(Main.player[number])) return; // a routed heal is the soldier's, not the commander's
			if (_depth == 0 || (msgType != 4 && msgType != 13)) { orig(msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7); return; }
			int first = -1;
			for (int k = 0; k < _depth; k++) if (_stack[k].Moved && _stack[k].P.whoAmI == number) { first = k; break; }
			if (first < 0) { orig(msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7); return; }
			ref Frame b = ref _stack[first]; Player p = b.P;
			Vector2 pos = p.position, vel = p.velocity, old = p.oldPosition; int dir = p.direction;
			NetGuards++;
			p.position = b.Pos; p.velocity = b.Vel; p.oldPosition = b.Old; p.direction = b.Dir;
			try { orig(msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7); }
			finally { p.position = pos; p.velocity = vel; p.oldPosition = old; p.direction = dir; }
		} private static int _hl_SendDataDetour_5;

		// ---- Thorium local-player cache: PlayerHelper.SetLocalThoriumPlayer (Thorium's own API; reflection so Levy has no compile-time Thorium dependency) ----
		private static MethodInfo _setLocal; private static ModPlayer _tpl; private static bool _tried;
		private static bool EnsureThorium()
		{
			if (!_tried)
			{
				_tried = true;
				try
				{
					if (ModLoader.TryGetMod("ThoriumMod", out Mod th))
					{
						_setLocal = th.Code.GetType("ThoriumMod.Utilities.PlayerHelper")?.GetMethod("SetLocalThoriumPlayer", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
						foreach (ModPlayer m in ModContent.GetContent<ModPlayer>()) if (m.Mod == th && m.GetType().Name == "ThoriumPlayer") _tpl = m;
					}
				}
				catch (Exception) { }
			}
			if (!_logged) { _logged = true; ModContent.GetInstance<OwnerPuppet>()?.Mod.Logger.Info($"OWNERPUPPET: thorium cache api found={_setLocal != null} playerType found={_tpl != null}"); }
			return _setLocal != null && _tpl != null;
		}
		private static bool _logged;

		/// <summary>HARNESS ONLY: a real client's local player update calls SetLocalThoriumPlayer(commander); the stand-in commander on a dedicated server never does, so do it here.</summary>
		internal static void EmulateClientCache(Player commander)
		{
			try { if (EnsureThorium()) _setLocal.Invoke(null, new object[] { commander.GetModPlayer(_tpl) }); } catch (Exception) { }
		}

		private sealed class ThToken { public ModPlayer Prev; }
		/// <summary>The ModPlayer this class last installed in Thorium's cache (null = nothing of ours installed, the commander's own is there).</summary>
		private static ModPlayer _curTp;

		/// <summary>Points Thorium's local-player cache at the dummy's ThoriumPlayer (the dummy's whoAmI is Main.myPlayer for the call, which SetLocalThoriumPlayer requires). Returns a token for RestoreThorium; null when off / unavailable.</summary>
		internal static object SwapThorium(Player dummy, bool force = false)
		{
			if ((!force && (Mask & 8) == 0) || dummy == null) return null;
			try
			{
				if (!EnsureThorium()) return null;
				ModPlayer tp = dummy.GetModPlayer(_tpl);
				int w = dummy.whoAmI; dummy.whoAmI = Main.myPlayer; // SetLocalThoriumPlayer only accepts the player whose whoAmI == Main.myPlayer
				try { _setLocal.Invoke(null, new object[] { tp }); ThSwaps++; } finally { dummy.whoAmI = w; }
				var tok = new ThToken { Prev = _curTp }; _curTp = tp;
				return tok;
			}
			catch (Exception) { return null; }
		}

		/// <summary>Puts the cache back to what it was before the matching SwapThorium: an enclosing soldier's when nested, else the commander's.</summary>
		internal static void RestoreThorium(object token)
		{
			if (token is not ThToken tok || !EnsureThorium()) return;
			try
			{
				ModPlayer target = tok.Prev;
				if (target == null) { Player c = Main.player[Main.myPlayer]; if (c != null && c.active) target = c.GetModPlayer(_tpl); }
				if (target != null)
				{
					Player o = target.Player; int w = o.whoAmI; bool fix = w != Main.myPlayer; if (fix) o.whoAmI = Main.myPlayer;
					try { _setLocal.Invoke(null, new object[] { target }); } finally { if (fix) o.whoAmI = w; }
				}
			}
			catch (Exception) { }
			finally { _curTp = tok.Prev; }
		}
	}

	/// <summary>Lab (LEVY_SWAPPROF=1): what the owner swaps around a soldier's shot cost, against the shot's whole update (Stopwatch ticks).</summary>
	internal static class SwapProf
	{
		internal static bool On = System.Environment.GetEnvironmentVariable("LEVY_SWAPPROF") == "1";
		internal static long UpdSwap, UpdTotal, UpdN, DamSwap, DamN, EnterMiss, GuardSwap, GuardN, ViewSwap, ViewN, SkipSwap, SkipN;
		internal static void Reset() { UpdSwap = UpdTotal = UpdN = DamSwap = DamN = EnterMiss = GuardSwap = GuardN = ViewSwap = ViewN = SkipSwap = SkipN = 0; }
	}
}
