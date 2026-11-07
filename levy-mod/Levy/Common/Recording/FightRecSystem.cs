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

		public override void PostUpdateEverything()
		{
			if (!FightRecorder.On) FightRecAuto.Update(); // LEVY_REC_DIR unset: one string test and nothing else
			if (FightRecorder.On) FightRecorder.Tick();
		}

		public override void OnWorldUnload()
		{
			if (FightRecorder.On) FightRecorder.Stop("world unloaded");
		}

		public override void Unload()
		{
			if (FightRecorder.On) FightRecorder.Stop("unloaded");
		}
	}

	/// <summary>Levy-side hooks for the recorder: names the hostile projectile before it strikes a levy and logs the lethal blow. Does nothing while the recorder is off.</summary>
	public class FightRecLevyHooks : GlobalNPC
	{
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => lateInstantiation && entity.ModNPC is SoldierNPC;

		public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
		{
			if (FightRecorder.On && projectile.hostile) FightRecorder.NoteLevyProjectile(npc, projectile);
		}

		public override bool CheckDead(NPC npc)
		{
			if (FightRecorder.On) FightRecorder.NoteLevyDeath(npc);
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
			if (FightRecorder.On) FightRecorder.NoteBossSource(npc, "item", item.type, item.Name);
		}

		public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
		{
			if (FightRecorder.On) FightRecorder.NoteBossSource(npc, "proj", projectile.type, Terraria.Lang.GetProjectileName(projectile.type).Value);
		}

		public override void OnKill(NPC npc)
		{
			if (FightRecorder.On) FightRecorder.NoteBossKill(npc);
		}
	}

	/// <summary>Gives every NPC a spawn serial while LEVY_REC_DIR is set, so headless recording can tell one trial's boss from the next even when it reuses the same slot. Absent entirely when the variable is unset.</summary>
	public class FightRecSpawnTag : GlobalNPC
	{
		private static int _counter;
		public int Serial;
		public override bool InstancePerEntity => true;
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => !string.IsNullOrEmpty(FightRecAuto.Dir);
		public override void OnSpawn(NPC npc, Terraria.DataStructures.IEntitySource source)
		{
			// a segment, servant or add spawned by an NPC belongs to its parent's fight (Eater of Worlds spawns its body from the head's AI): same serial, so a worm's segments are not mistaken for the next trial's boss
			if (source is Terraria.DataStructures.EntitySource_Parent par && par.Entity is NPC parent && parent.active && SerialOf(parent) > 0) { Serial = SerialOf(parent); return; }
			Serial = ++_counter;
		}
		public static int SerialOf(NPC n) => n.TryGetGlobalNPC(out FightRecSpawnTag t) ? t.Serial : 0;
	}

	public class FightRecPlayer : ModPlayer
	{
		public override void OnHurt(Player.HurtInfo info)
		{
			if (!FightRecorder.On) return;
			var src = info.DamageSource;
			FightRecorder.NoteHit(Player, info.Damage, src.SourceNPCIndex, src.SourceProjectileType, src.SourceOtherIndex);
		}
	}

	/// <summary>/levyrec start [name], /levyrec stop, /levyrec status. Records the local player's fight to Levy/recordings in the tModLoader save folder.</summary>
	public class LevyRecCommand : ModCommand
	{
		public override CommandType Type => CommandType.Chat;
		public override string Command => "levyrec";
		public override string Description => "Fight recorder. /levyrec start [name] begins, /levyrec stop ends and prints the file path.";
		public override void Action(CommandCaller caller, string input, string[] args)
		{
			if (Main.dedServ) { caller.Reply("levyrec runs on a client.", Color.OrangeRed); return; }
			string a = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
			if (a == "start")
			{
				string name = args.Length > 1 ? string.Join("_", args, 1, args.Length - 1) : null;
				string path = FightRecorder.Start(Main.LocalPlayer, name, out string err);
				if (path == null) caller.Reply("levyrec: " + err, Color.OrangeRed);
				else caller.Reply("levyrec: recording to " + path + " (stops on boss death, /levyrec stop, or after 10 minutes)", Color.LightGreen);
			}
			else if (a == "stop")
			{
				string path = FightRecorder.Stop("stopped by command");
				caller.Reply(path == null ? "levyrec: not recording." : "levyrec: saved " + path, path == null ? Color.OrangeRed : Color.LightGreen);
			}
			else caller.Reply(FightRecorder.On ? "levyrec: recording " + (FightRecorder.Elapsed / 60) + "s to " + FightRecorder.Path : "levyrec: off. /levyrec start [name]", Color.LightSkyBlue);
		}
	}
}
