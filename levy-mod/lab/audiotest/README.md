FNA/FAudio churn test (audiobug). Mimics SoundPlayer (CreateInstance + Pitch/Pan/Volume + Play, MaxInstances=1 replace-oldest per style) at N plays/s beside a looped "music" tone, SDL disk audio driver.
Build: dotnet build -c Release -o out (net10 SDK), copy tml-bin/tml/Libraries/Native/Linux/*.so* into out, symlink libSDL2.so libFAudio.so libFNA3D.so.
Run: SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=disk SDL_DISKAUDIOFILE=o.raw LD_LIBRARY_PATH=. dotnet audiotest.dll <playsPerSec> 25 10
Result 2026-11-10: 30, 250 and 600 plays/s all mix cleanly (no 20 ms window under half the median RMS at 250 and 600); one play costs 17 us median.
