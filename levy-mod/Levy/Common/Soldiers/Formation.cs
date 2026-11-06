namespace Levy.Common.Soldiers
{
	/// <summary>The squad stance behind the Spread out / Close up button. One button cycles Normal, Spread, Close up, Normal...</summary>
	public enum FormationMode : byte { Normal = 0, Spread = 1, CloseUp = 2 }

	public static class Formation
	{
		public static FormationMode Of(SoldierRecord rec) => rec.SpreadOut ? FormationMode.Spread : rec.CloseUp ? FormationMode.CloseUp : FormationMode.Normal;

		/// <summary>Sets the stance; the two flags are mutually exclusive.</summary>
		public static void Apply(SoldierRecord rec, FormationMode mode)
		{
			rec.SpreadOut = mode == FormationMode.Spread;
			rec.CloseUp = mode == FormationMode.CloseUp;
		}

		/// <summary>The stance most of the squad is in (a strict majority); Normal when none has one.</summary>
		public static FormationMode Majority(int total, int spread, int close)
		{
			if (total <= 0) return FormationMode.Normal;
			if (spread * 2 > total) return FormationMode.Spread;
			if (close * 2 > total) return FormationMode.CloseUp;
			return FormationMode.Normal;
		}

		/// <summary>Normal goes to Spread, Spread goes to Close up, Close up goes back to Normal.</summary>
		public static FormationMode Next(FormationMode mode) => mode == FormationMode.Normal ? FormationMode.Spread : mode == FormationMode.Spread ? FormationMode.CloseUp : FormationMode.Normal;
	}
}
