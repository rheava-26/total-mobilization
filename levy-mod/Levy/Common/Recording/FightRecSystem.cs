using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Levy.Content.NPCs;

namespace Levy.Common.Recording
{
	/// <summary>Hooks for the fight recorder. Off by default; the per-tick cost while off is one static bool test.</summary>
	public class FightRecSystem : ModSystem
	{
		public override void PostSetupContent()
		{
			string fail = FightRecFormat.SelfCheck();
			if (fail == null) Mod.Logger.Info("[FightRec] self-check ok (frame, event and arena round-trip)");
			else Mod.Logger.Error("[FightRec] self-check FAILED: " + fail);
		}

		public override void PostUpdateEverything() => Step();

		/// <summary>One game tick of the recorder: show queued chat lines, start an automatic recording when one is due, sample, stop when the fight is over.</summary>
		internal static void Step()
		{
			FightRecAuto.Pump();
			if (!FightRecorder.On) FightRecAuto.Update(); // setting off and LEVY_REC_DIR unset: a config read and an enum test
			if (FightRecorder.On) { FightRecorder.Tick(); if (FightRecorder.On) FightRecAuto.Watch(); }
			EventRecorder.Step(); // the event recording runs beside the boss recorder (format 12)
		}

		public override void OnWorldUnload()
		{
			if (FightRecorder.On) FightRecorder.Stop("world unloaded"); // finishes the file before the world goes (the writer is joined)
			if (EventRecorder.On) EventRecorder.Stop("world unloaded");
			FightRecAuto.ResetMessages();
		}

		public override void Unload()
		{
			if (FightRecorder.On) FightRecorder.Stop("unloaded");
			if (EventRecorder.On) EventRecorder.Stop("unloaded");
		}
	}

	/// <summary>Levy-side hooks for the recorder: names the hostile projectile before it strikes a levy and logs the lethal blow. Does nothing while the recorder is off.</summary>
	public class FightRecLevyHooks : GlobalNPC
	{
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => lateInstantiation && entity.ModNPC is SoldierNPC;

		public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
		{
			if (FightRecorder.On && projectile.hostile) FightRecorder.NoteLevyProjectile(npc, projectile);
			if (EventRecorder.On && projectile.hostile) EventRecorder.NoteLevyProjectile(npc, projectile);
		}

		public override bool CheckDead(NPC npc)
		{
			if (FightRecorder.On) FightRecorder.NoteLevyDeath(npc);
			if (EventRecorder.On) EventRecorder.NoteLevyDeath(npc);
			return true;
		}
	}

	/// <summary>Tells the recorder how a fight boss was hit and that it died (format 4 bdeath / bdespawn). Does nothing while the recorder is off.</summary>
	public class FightRecBossHooks : GlobalNPC
	{
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => lateInstantiation && !(entity.ModNPC is SoldierNPC) && !entity.friendly && !entity.townNPC;

		public override void HitEffect(NPC npc, NPC.HitInfo hit)
		{
			if (FightRecorder.On) FightRecorder.NoteBossHit(npc, hit.Damage);
		}

		public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone)
		{
			if (EventRecorder.On) EventRecorder.NoteDamage(npc, player, null, item, damageDone, hit.Crit);
			if (!FightRecorder.On) return;
			FightRecorder.NoteBossSource(npc, "item", item.type, item.Name);
			FightRecorder.NoteDamage(npc, player, null, item, damageDone, hit.Crit);
		}

		public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
		{
			if (EventRecorder.On) EventRecorder.NoteDamage(npc, null, projectile, null, damageDone, hit.Crit);
			if (!FightRecorder.On) return;
			FightRecorder.NoteBossSource(npc, "proj", projectile.type, Terraria.Lang.GetProjectileName(projectile.type).Value);
			FightRecorder.NoteDamage(npc, null, projectile, null, damageDone, hit.Crit);
		}

		public override void OnKill(NPC npc)
		{
			if (FightRecorder.On) FightRecorder.NoteBossKill(npc);
			if (EventRecorder.On) EventRecorder.NoteKill(npc);
		}
	}

	/// <summary>Gives every NPC a spawn serial while LEVY_REC_DIR is set (and in a self-check world, so the world checks drive the same path), so headless recording can tell one trial's boss from the next even when it reuses the same slot.
	/// Absent entirely otherwise. The serials are read only by a headless recording (<see cref="SerialOf"/> is 0 for every other recording, so a player's recordings behave the same in a self-check world).</summary>
	public class FightRecSpawnTag : GlobalNPC
	{
		private static int _counter;
		public int Serial;
		/// <summary>The tags exist: LEVY_REC_DIR is set, or this is a self-check world (LEVY_SELFCHECK_WORLD=1).</summary>
		internal static readonly bool Tagging = !string.IsNullOrEmpty(FightRecAuto.Dir) || System.Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") == "1";
		public override bool InstancePerEntity => true;
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => Tagging;
		public override void OnSpawn(NPC npc, Terraria.DataStructures.IEntitySource source)
		{
			// a segment, servant or add spawned by an NPC belongs to its parent's fight (Eater of Worlds spawns its body from the head's AI): same serial, so a worm's segments are not mistaken for the next trial's boss.
			// An NPC a projectile spawns takes the serial of the NPC that made the projectile (Anahita's LeviathanSpawner summons the Leviathan).
			if (source is Terraria.DataStructures.EntitySource_Parent par)
			{
				if (par.Entity is NPC parent && parent.active && RawSerial(parent) > 0) { Serial = RawSerial(parent); return; }
				if (par.Entity is Projectile pr && pr.active && FightRecProjSerial.SerialOf(pr) > 0) { Serial = FightRecProjSerial.SerialOf(pr); return; }
			}
			Serial = ++_counter;
		}
		internal static int RawSerial(NPC n) => n.TryGetGlobalNPC(out FightRecSpawnTag t) ? t.Serial : 0;
		/// <summary>The spawn serial a headless recording reads; 0 while no headless recording mode is on.</summary>
		public static int SerialOf(NPC n) => FightRecorder.Headless ? RawSerial(n) : 0;
	}

	/// <summary>A projectile an NPC (or a projectile of one) makes carries that NPC's spawn serial, where <see cref="FightRecSpawnTag"/> runs: a boss a projectile summons belongs to the fight of the boss that made it, and the recorder
	/// holds a fight open while such a no-damage summoner is still out.</summary>
	public class FightRecProjSerial : GlobalProjectile
	{
		public int Serial;
		public override bool InstancePerEntity => true;
		public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => FightRecSpawnTag.Tagging;
		public override void OnSpawn(Projectile projectile, Terraria.DataStructures.IEntitySource source)
		{
			if (source is not Terraria.DataStructures.EntitySource_Parent par) return;
			if (par.Entity is NPC n && n.active) Serial = FightRecSpawnTag.RawSerial(n);
			else if (par.Entity is Projectile p && p.active) Serial = SerialOf(p);
		}
		public static int SerialOf(Projectile p) => p.TryGetGlobalProjectile(out FightRecProjSerial t) ? t.Serial : 0;
	}

	/// <summary>Remembers which item fired a projectile (and which one fired the projectile that spawned it), while a recording runs, so the report can name the weapon of a hit and not only the item in the hand. Format 10.</summary>
	public class FightRecProjTag : GlobalProjectile
	{
		public override bool InstancePerEntity => true;
		public int SrcItem;
		public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => lateInstantiation && entity.friendly && !entity.hostile;
		public override void OnSpawn(Projectile projectile, Terraria.DataStructures.IEntitySource source)
		{
			if (!FightRecorder.On && !EventRecorder.On) return;
			if (source is Terraria.DataStructures.EntitySource_ItemUse use && use.Item != null && !use.Item.IsAir) SrcItem = use.Item.type;
			else if (source is Terraria.DataStructures.EntitySource_Parent par && par.Entity is Projectile parent && parent.TryGetGlobalProjectile(out FightRecProjTag pt)) SrcItem = pt.SrcItem;
		}
	}

	public class FightRecPlayer : ModPlayer
	{
		public override void Kill(double damage, int hitDirection, bool pvp, Terraria.DataStructures.PlayerDeathReason damageSource)
		{
			if (FightRecorder.On) FightRecorder.NoteKill(Player, damage, damageSource);
		}

		public override void OnHurt(Player.HurtInfo info)
		{
			if (!FightRecorder.On) return;
			var src = info.DamageSource;
			FightRecorder.NoteHit(Player, info.Damage, src.SourceNPCIndex, src.SourceProjectileType, src.SourceOtherIndex);
		}
	}

	/// <summary>/levyrec start [name], /levyrec stop, /levyrec status, /levyrec folder. Records the local player's fight into the recording folder (Levy/Recordings in the tModLoader save folder, or the one set in the Fight recorder config).</summary>
	public class LevyRecCommand : ModCommand
	{
		public override CommandType Type => CommandType.Chat;
		public override string Command => "levyrec";
		public override string Usage => "/levyrec [start [name]|stop|status|folder]";
		public override string Description => "Fight recorder. /levyrec start [name] begins, /levyrec stop ends and prints the file path, /levyrec folder prints the recording folder.";
		public override void Action(CommandCaller caller, string input, string[] args)
		{
			if (Main.dedServ) { caller.Reply("levyrec runs on a client.", Color.OrangeRed); return; }
			string a = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
			if (a == "start")
			{
				string name = args.Length > 1 ? string.Join("_", args, 1, args.Length - 1) : null;
				FightRecorder.AutoKind = 0;
				string path = FightRecorder.Start(Main.LocalPlayer, name, out string err);
				if (path == null) caller.Reply("levyrec: " + err, Color.OrangeRed);
				else caller.Reply("levyrec: recording (stops on boss death, /levyrec stop, or after 10 minutes)", Color.LightGreen);
			}
			else if (a == "stop")
			{
				string path = FightRecorder.Stop("stopped by command");
				caller.Reply(path == null ? "levyrec: not recording." : "levyrec: saved " + path, path == null ? Color.OrangeRed : Color.LightGreen);
			}
			else if (a == "folder")
			{
				string cfg = FightRecConfig.Get?.RecordingFolder;
				string d = FightRecFolder.Resolve(cfg, FightRecFolder.DefaultDir, out string warn);
				caller.Reply(d != null ? "levyrec: recordings go to " + d : "levyrec: " + warn, d != null ? Color.LightSkyBlue : Color.OrangeRed);
				if (d != null && warn != null) caller.Reply("levyrec: " + warn, Color.OrangeRed);
			}
			else caller.Reply(FightRecorder.On ? "levyrec: recording " + (FightRecorder.Elapsed / 60) + "s" + (FightRecorder.Auto != 0 ? " (automatic)" : "") : "levyrec: off. Automatic recording: " + FightRecAuto.CurrentMode() + ". /levyrec start [name]", Color.LightSkyBlue);
		}
	}
}
