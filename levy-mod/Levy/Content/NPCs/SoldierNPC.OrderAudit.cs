using System.Text;
using Terraria;
using Levy.Common;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// What "this levy took the order" means, as a rule that can be checked: the record holds the order, the body is not holding a posture the order replaced, and a body whose state another module owns (fear, desertion, a fight) is
	/// allowed to stay in that state. Used by <see cref="Levy.Common.Systems.OrderAudit"/> (a few seconds after every order, on the live army) and by the boot self-check.
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>The state the order puts an idle body in (Brain_OnOrder), or null for an order that has none.</summary>
		internal static SoldierState? StateAfterOrder(SoldierOrder order) => order switch
		{
			SoldierOrder.Follow => SoldierState.Follow,
			SoldierOrder.Hold => SoldierState.Idle,
			SoldierOrder.ReturnToBarracks => SoldierState.ReturnHome,
			SoldierOrder.SearchDestroy => SoldierState.SearchDestroy,
			SoldierOrder.FreeRein => SoldierState.FreeRein,
			_ => null,
		};

		/// <summary>
		/// Whether this body's posture agrees with the standing order <paramref name="order"/> some time after it was given. Shaken / Routed / Desert / Dead belong to other modules and always agree (the record still holds the order);
		/// a fight (Engage), a haul and a resupply walk are allowed to outrank everything but are never a seated or a held posture left over from the old order. Returns the reason when it does not.
		/// </summary>
		internal bool ObeysOrder(SoldierOrder order, out string why)
		{
			why = null;
			if (Record == null) { why = "no record"; return false; }
			if (Record.Order != order) { why = "record holds " + Record.Order; return false; }
			if (State == SoldierState.Shaken || State == SoldierState.Routed || State == SoldierState.Desert || State == SoldierState.Dead || State == SoldierState.Mutiny) return true;
			if (HoldPosition != (order == SoldierOrder.Hold)) { why = "HoldPosition=" + HoldPosition; return false; }
			// a calm levy at its Hold post, or following a commander who stands still, sits down again a few seconds after the order stood it up: that is the post, not a missed order
			bool mayRest = order == SoldierOrder.Hold || (order == SoldierOrder.Follow && Commander != null);
			if (Seated && !mayRest) { why = "still seated"; return false; }
			if (State == SoldierState.Engage || State == SoldierState.Haul || State == SoldierState.Resupply) return true;
			SoldierState? want = StateAfterOrder(order);
			if (want.HasValue && State == want.Value) return true;
			// the brain re-derives the state from the order every think: an idle spot (arrived home, nobody to follow, a hold post) is also what the order asks for there
			if (State == SoldierState.Idle && (order == SoldierOrder.Hold || order == SoldierOrder.ReturnToBarracks || Commander == null || Home == null)) return true;
			// Follow with no commander online falls back to the barracks (OrderState)
			if (order == SoldierOrder.Follow && Commander == null && State == SoldierState.ReturnHome) return true;
			why = "state " + State + " for order " + order;
			return false;
		}

		/// <summary>One line of everything that can keep a body from acting on an order, for the audit log.</summary>
		internal string OrderDiagnosis()
		{
			var sb = new StringBuilder();
			sb.Append("state=").Append(State).Append(" order=").Append(Record?.Order).Append(" hold=").Append(HoldPosition).Append(" seated=").Append(Seated).Append(" target=").Append(TargetIndex)
				.Append(" goal=").Append(HasMoveGoal).Append(" why=").Append(StillReason()).Append(" fear=").Append(Fear.ToString("0")).Append(" canUseWeapon=").Append(CanUseHeldWeapon())
				.Append(" commander=").Append(Commander != null).Append(" home=").Append(Home != null).Append(" holdFire=").Append(Record?.HoldFire).Append(" mutinous=").Append(Record?.Mutinous)
				.Append(" mindControlled=").Append(MindControlled).Append(" life=").Append(NPC.life).Append('/').Append(NPC.lifeMax).Append(" tile=").Append((int)(NPC.Center.X / 16f)).Append(',').Append((int)(NPC.Center.Y / 16f));
			return sb.ToString();
		}
	}
}
