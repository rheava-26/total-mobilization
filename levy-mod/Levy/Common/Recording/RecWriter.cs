using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
	/// The gzip stream uses the smallest-size level (zlib 9): the writer has its own thread, and the files are about 4% smaller than at the default level.
	/// PART FILES (gzip only): once the current part passes the part size, <see cref="PartDue"/> is set; the recorder answers with <see cref="StartPart"/> (the header of the next part), and every line queued after that
	/// goes to NAME_partN.levyrec. On close every part is moved to its final name (the first one to the name given, part N to that name with _partN before the extension). The size cap counts all parts.
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
		private readonly long _maxBytes, _partBytes;
		private long _closedBytes;
		private int _part = 1;
		private volatile bool _partDue;
		private readonly List<string> _temps = new();
		/// <summary>Marks a part switch in the queue (no recording line starts with it).</summary>
		private const char PartMark = '\u0001';
		private long _queuedChars, _rawBytes, _fileBytes, _dropped;
		private volatile bool _closing, _done, _full;
		private volatile string _error;
		private Action<string> _onDone;
		private volatile int _flushAsk, _flushSeen;
		private string _renameTo;

		/// <summary>The compressed (or plain) size on disk has passed the cap: the recorder should stop and close.</summary>
		public bool Full => _full;
		/// <summary>The part being written has passed the part size: the recorder should call <see cref="StartPart"/> now.</summary>
		public bool PartDue => _partDue;
		/// <summary>The part the next lines go to (1 = the first file).</summary>
		public int Part => _part;
		public string Error => _error;
		public bool Done => _done;
		public long Dropped => Interlocked.Read(ref _dropped);
		public long RawBytes => Interlocked.Read(ref _rawBytes);
		/// <summary>Compressed (or plain) bytes on disk, every part together.</summary>
		public long FileBytes => Interlocked.Read(ref _fileBytes);
		/// <summary>Where the bytes are right now (the .part name while writing).</summary>
		public string TempPath => _tempPath;

		/// <summary>Opens a recording. False with the reason when the folder is missing or the file cannot be created (no exception for the usual cases).</summary>
		public static bool TryOpen(string path, bool gzip, long maxBytes, out RecWriter w, out string why, long partBytes = 0)
		{
			w = null; why = null;
			string dir = Path.GetDirectoryName(path);
			if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) { why = "the folder " + dir + " does not exist"; return false; }
			try { w = new RecWriter(path, gzip, maxBytes, partBytes); return true; }
			catch (Exception e) { why = e.Message; return false; }
		}

		/// <param name="path">The file written to. For a gzip recording it is written under path + ".part" and moved to <paramref name="path"/> (or the name given to Close) when finished.</param>
		public RecWriter(string path, bool gzip, long maxBytes, long partBytes = 0)
		{
			_gzip = gzip; _maxBytes = maxBytes <= 0 ? long.MaxValue : maxBytes; _partBytes = gzip && partBytes > 0 ? partBytes : 0;
			_finalPath = path; _tempPath = gzip ? path + ".part" : path; _temps.Add(_tempPath);
			_thread = new Thread(Run) { IsBackground = true, Name = "LevyFightRec" };
			// create the file here so an unwritable folder fails at Start, on the caller's thread, not later
			_fs = new FileStream(_tempPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16, FileOptions.None);
			_thread.Start();
		}

		private FileStream _fs;

		/// <summary>NAME.levyrec -> NAME_partN.levyrec (N >= 2; N 1 is the name itself).</summary>
		public static string PartName(string path, int n) => RecParts.PartName(path, n);

		/// <summary>Every line queued after this call goes to the next part file, which starts with <paramref name="header"/>. Only for a gzip recording with a part size; does nothing otherwise.</summary>
		public void StartPart(string header)
		{
			if (_closing || _partBytes <= 0 || header == null) return;
			_partDue = false;
			Interlocked.Add(ref _queuedChars, header.Length + 2);
			_q.Enqueue(PartMark + header);
			_sig.Release();
		}

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
				if (_gzip) { gz = new GZipStream(raw, CompressionLevel.SmallestSize, leaveOpen: true); target = gz; }
				w = new StreamWriter(target, new UTF8Encoding(false), 1 << 16) { NewLine = "\n" };
				while (true)
				{
					_sig.Wait(Math.Min(500, FlushMs));
					int ask = _flushAsk; // lines queued before a Flush call are drained below, then flushed
					int n = 0;
					while (_q.TryDequeue(out string line))
					{
						Interlocked.Add(ref _queuedChars, -(line.Length + 1));
						if (_error == null && line.Length > 0 && line[0] == PartMark)
						{
							// the next part: close this gzip stream and file, open NAME_partN.levyrec.part, start it with the header
							try
							{
								w.Flush(); w.Dispose(); w = null; gz = null; raw.Flush(); _closedBytes += raw.Length; raw.Dispose(); raw = null;
								_part++;
								string tp = PartName(_finalPath, _part) + ".part"; _temps.Add(tp);
								raw = new FileStream(tp, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16, FileOptions.None);
								gz = new GZipStream(raw, CompressionLevel.SmallestSize, leaveOpen: true);
								w = new StreamWriter(gz, new UTF8Encoding(false), 1 << 16) { NewLine = "\n" };
								string h = line.Substring(1); w.WriteLine(h); Interlocked.Add(ref _rawBytes, h.Length + 1); n++;
							}
							catch (Exception e) { _error = e.Message; }
							continue;
						}
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
							long len = raw.Length; Interlocked.Exchange(ref _fileBytes, _closedBytes + len);
							if (_closedBytes + len > _maxBytes) _full = true;
							if (_partBytes > 0 && len > _partBytes) _partDue = true;
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
					try { Interlocked.Exchange(ref _fileBytes, _closedBytes + new FileInfo(_temps[_temps.Count - 1]).Length); } catch { }
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
				for (int i = 0; i < _temps.Count; i++)
				{
					string from = _temps[i], dest = PartName(to, i + 1);
					if (!File.Exists(from)) continue;
					if (!string.Equals(dest, from, StringComparison.Ordinal)) { if (File.Exists(dest)) File.Delete(dest); File.Move(from, dest); }
					if (i == 0) result = to;
				}
			}
			catch (Exception e) { _error ??= e.Message; result = _gzip ? _tempPath : result; }
			_done = true;
			try { _onDone?.Invoke(result); } catch (Exception e) { Levy.Instance?.Logger.Warn("[FightRec] finish callback failed: " + e.Message); }
		}
	}
}
