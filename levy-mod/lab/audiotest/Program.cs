using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework.Audio;

// Mimics tML SoundPlayer: each play = CreateInstance + Pitch/Pan/Volume + Play; MaxInstances=1 ReplaceOldest per style (Stop(immediate)); stopped instances disposed in an Update pass.
// args: playsPerSecond styles seconds
class P
{
	static SoundEffect Make(int ms, double f, bool noise, int rate = 44100)
	{
		int n = rate * ms / 1000; var b = new byte[n * 2]; var r = new Random((int)f);
		for (int i = 0; i < n; i++)
		{
			double env = Math.Exp(-5.0 * i / n);
			double v = noise ? (r.NextDouble() * 2 - 1) : Math.Sin(2 * Math.PI * f * i / rate);
			short s = (short)(v * env * 9000); b[2 * i] = (byte)(s & 255); b[2 * i + 1] = (byte)(s >> 8);
		}
		return new SoundEffect(b, rate, AudioChannels.Mono);
	}
	static void Main(string[] a)
	{
		var fp = typeof(Microsoft.Xna.Framework.Game).Assembly.GetType("Microsoft.Xna.Framework.FNAPlatform"); foreach (var mn in new[]{"ProgramInit"}) { var m = fp.GetMethod(mn, System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic); Console.WriteLine(mn+" "+(m!=null)); m?.Invoke(null,null); } int pps = int.Parse(a[0]), styles = int.Parse(a[1]), secs = int.Parse(a[2]);
		var fx = new SoundEffect[styles];
		for (int i = 0; i < styles; i++) fx[i] = Make(150 + (i * 53) % 400, 300 + i * 37, i % 2 == 0);
		// "music": a steady looped tone
		var mus = Make(1000, 440, false); var music = mus.CreateInstance(); music.IsLooped = true; music.Volume = 0.3f; music.Play();
		var cur = new SoundEffectInstance[styles]; var live = new List<SoundEffectInstance>();
		var rnd = new Random(1); var sw = Stopwatch.StartNew(); var cost = new List<double>(); long plays = 0; double next = 0; int maxLive = 0;
		double tick = 1.0 / 60; double t = 0;
		while (sw.Elapsed.TotalSeconds < secs)
		{
			double now = sw.Elapsed.TotalSeconds;
			while (next <= now)
			{
				next += 1.0 / pps; int s = rnd.Next(styles);
				long t0 = Stopwatch.GetTimestamp();
				if (cur[s] != null && cur[s].State == SoundState.Playing) cur[s].Stop(true);
				var inst = fx[s].CreateInstance(); inst.Pitch = (float)(rnd.NextDouble() * 0.24 - 0.12); inst.Pan = (float)(rnd.NextDouble() * 2 - 1); inst.Volume = 0.6f; inst.Play();
				cur[s] = inst; live.Add(inst); plays++;
				cost.Add((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency);
			}
			if (now - t >= tick)
			{
				t = now;
				for (int i = 0; i < live.Count; i++) if (live[i].State == SoundState.Stopped) { live[i].Dispose(); live.RemoveAt(i); i--; }
				if (live.Count > maxLive) maxLive = live.Count;
			}
			System.Threading.Thread.Sleep(1);
		}
		cost.Sort();
		Console.WriteLine($"plays={plays} pps={pps} styles={styles} maxLive={maxLive} playCostMs median={cost[cost.Count / 2]:0.000} p99={cost[(int)(cost.Count * 0.99)]:0.000} max={cost[cost.Count - 1]:0.000} total={cost.Sum():0}ms");
	}
}
static class Ext { public static double Sum(this List<double> l) { double s = 0; foreach (var x in l) s += x; return s; } }
