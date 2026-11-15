using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace Levy.Common.Recording
{
	/// <summary>
	/// Writes the lines of one recording on its own thread, so the game thread only queues a string. The file is JSON lines, optionally gzip-compressed (the .levyrec files). The gzip stream is
	/// sync-flushed every couple of seconds, so a crash leaves a file that still reads up to the last flush. The writer stops accepting lines once the compressed size passes the cap
	/// (<see cref="Full"/>) or the queue holds more than <see cref="MaxQueuedChars"/> (a stalled disk); lines written with force always go in, so the closing records are never lost.
	/// A recording that cannot be written (disk full, folder gone) sets <see cref="Error"/> and the recorder stops itself.
	/// </summary>
	internal sealed class RecWriter
	{
		public const long MaxQueuedChars = 48L * 1024 * 1024;
		/// <summary>How often the gzip stream is sync-flushed (and the size cap checked). Self-checks shorten it.</summary>
		public static int FlushMs = 2000;
		private readonly ConcurrentQueue<string> _q = new();
		private readonly SemaphoreSlim _sig = new(0);
		private readonly Thread _thread;
		private readonly string _tempPath, _finalPath;
		private readonly bool _gzip;
		private readonly long _maxBytes;
		private long _queuedChars, _rawBytes, _fileBytes, _dropped;
		private volatile bool _closing, _done, _full;
		private volatile string _error;
		private Action<string> _onDone;
		private volatile int _flushAsk, _flushSeen;
		private string _renameTo;

		/// <summary>The compressed (or plain) size on disk has passed the cap: the recorder should stop and close.</summary>
		public bool Full => _full;
		public string Error => _error;
		public bool Done => _done;
		public long Dropped => Interlocked.Read(ref _dropped);
		public long RawBytes => Interlocked.Read(ref _rawBytes);
		public long FileBytes => Interlocked.Read(ref _fileBytes);
		/// <summary>Where the bytes are right now (the .part name while writing).</summary>
		public string TempPath => _tempPath;

		/// <summary>Opens a recording. False with the reason when the folder is missing or the file cannot be created (no exception for the usual cases).</summary>
		public static bool TryOpen(string path, bool gzip, long maxBytes, out RecWriter w, out string why)
		{
			w = null; why = null;
			string dir = Path.GetDirectoryName(path);
			if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) { why = "the folder " + dir + " does not exist"; return false; }
			try { w = new RecWriter(path, gzip, maxBytes); return true; }
			catch (Exception e) { why = e.Message; return false; }
		}

		/// <param name="path">The file written to. For a gzip recording it is written under path + ".part" and moved to <paramref name="path"/> (or the name given to Close) when finished.</param>
		public RecWriter(string path, bool gzip, long maxBytes)
		{
			_gzip = gzip; _maxBytes = maxBytes <= 0 ? long.MaxValue : maxBytes;
			_finalPath = path; _tempPath = gzip ? path + ".part" : path;
			_thread = new Thread(Run) { IsBackground = true, Name = "LevyFightRec" };
			// create the file here so an unwritable folder fails at Start, on the caller's thread, not later
			_fs = new FileStream(_tempPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16, FileOptions.None);
			_thread.Start();
		}

		private FileStream _fs;

		public void WriteLine(string s, bool force = false)
		{
			if (_closing || s == null) return;
			if (!force && (_full || _error != null || Interlocked.Read(ref _queuedChars) > MaxQueuedChars)) { Interlocked.Increment(ref _dropped); return; }
			Interlocked.Add(ref _queuedChars, s.Length + 1);
			_q.Enqueue(s);
			_sig.Release();
		}

		/// <summary>
		/// Writes everything queued so far and sync-flushes the stream now, as the timer does every <see cref="FlushMs"/>; returns once the file holds it (false after <paramref name="waitMs"/> or when the writer has stopped).
		/// A copy of the open .part file taken after this returns reads back every line written before the call. Self-checks use it so they do not depend on how loaded the machine is.
		/// </summary>
		public bool Flush(int waitMs = 15000)
		{
			if (_closing || _done) return false;
			int ask = Interlocked.Increment(ref _flushAsk);
			_sig.Release();
			long until = Environment.TickCount64 + waitMs;
			while (_flushSeen < ask && !_done && Environment.TickCount64 < until) Thread.Sleep(1);
			return _flushSeen >= ask;
		}

		/// <summary>
		/// Ends the recording: everything queued is written, the file is closed and (gzip) moved to <paramref name="finalName"/> (null = the name given at construction). <paramref name="onDone"/> runs on the writer thread with the
		/// final path, or null when the file could not be finished. With <paramref name="wait"/> the call returns after that (at most <paramref name="waitMs"/>); without it the game thread carries on at once.
		/// </summary>
		public void Close(string finalName, Action<string> onDone, bool wait, int waitMs = 15000)
		{
			if (_closing) return;
			_renameTo = finalName ?? _finalPath; _onDone = onDone; _closing = true;
			_sig.Release();
			if (wait) _thread.Join(waitMs);
		}

		private void Run()
		{
			Stream raw = _fs; GZipStream gz = null; StreamWriter w = null; string result = null;
			long lastFlushMs = Environment.TickCount64;
			try
			{
				Stream target = raw;
				if (_gzip) { gz = new GZipStream(raw, CompressionLevel.Optimal, leaveOpen: true); target = gz; }
				w = new StreamWriter(target, new UTF8Encoding(false), 1 << 16) { NewLine = "\n" };
				while (true)
				{
					_sig.Wait(Math.Min(500, FlushMs));
					int ask = _flushAsk; // lines queued before a Flush call are drained below, then flushed
					int n = 0;
					while (_q.TryDequeue(out string line))
					{
						Interlocked.Add(ref _queuedChars, -(line.Length + 1));
						if (_error == null)
						{
							try { w.WriteLine(line); Interlocked.Add(ref _rawBytes, line.Length + 1); n++; }
							catch (Exception e) { _error = e.Message; }
						}
					}
					long now = Environment.TickCount64;
					if (_error == null && (now - lastFlushMs > FlushMs || _closing || ask != _flushSeen))
					{
						try
						{
							w.Flush(); gz?.Flush(); raw.Flush();
							long len = raw.Length; Interlocked.Exchange(ref _fileBytes, len);
							if (len > _maxBytes) _full = true;
						}
						catch (Exception e) { _error = e.Message; }
						lastFlushMs = now;
					}
					if (ask != _flushSeen) _flushSeen = ask; // also when the stream is in error: the caller must not wait for a flush that cannot come
					if (_closing && _q.IsEmpty) break;
				}
				if (_error == null)
				{
					try { w.Flush(); w.Dispose(); w = null; gz?.Dispose(); gz = null; } // a plain recording's StreamWriter closes the file with it
					catch (Exception e) { _error = e.Message; }
					try { Interlocked.Exchange(ref _fileBytes, new FileInfo(_tempPath).Length); } catch { }
				}
			}
			catch (Exception e) { _error ??= e.Message; }
			finally
			{
				try { w?.Dispose(); } catch { }
				try { gz?.Dispose(); } catch { }
				try { raw?.Dispose(); } catch { }
			}
			try
			{
				string to = _renameTo ?? _finalPath;
				if (File.Exists(_tempPath))
				{
					if (!string.Equals(to, _tempPath, StringComparison.Ordinal)) { if (File.Exists(to)) File.Delete(to); File.Move(_tempPath, to); }
					result = to;
				}
			}
			catch (Exception e) { _error ??= e.Message; result = _gzip ? _tempPath : result; }
			_done = true;
			try { _onDone?.Invoke(result); } catch (Exception e) { Levy.Instance?.Logger.Warn("[FightRec] finish callback failed: " + e.Message); }
		}
	}
}
