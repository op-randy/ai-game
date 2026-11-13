# Shooting-range audio sources

## Gunshot_1.wav, Gunshot_2.wav, Gunshot_3.wav

- User-supplied source: https://www.youtube.com/watch?v=XUYfaH1Z4FQ
- Video title: **Gun Fight (Shoot Out) - Sound Effect**.
- Retrieved 2026-11-12 with yt-dlp, explicitly selecting the installed Node runtime.
- Source audio: `Recorded/youtube-XUYfaH1Z4FQ.webm` (YouTube Opus audio).
- Original SHA-256: `dceaa9b5a712d5f06d8de3713739670921372c19cd02d454e33b1d2be1cda0b6`.
- Excerpts before leading-silence removal: 0.17–0.75 s, 3.48–4.16 s, 5.78–6.49 s.
- **Reuse/redistribution license is UNVERIFIED.** The metadata does not provide a
  reuse license. These are local prototype/audition samples requested by the user;
  obtain permission or replace them with suitably licensed recordings before
  publishing, distributing, or shipping a game containing them. Do not describe
  these as CC0, cleared assets, or verified recordings of a particular firearm.

## TargetHit.wav

- Original: `bing1.wav`, **Metal Impact Sounds**, by BMacZero / Brian MacIntosh.
- Source page: https://opengameart.org/content/metal-impact-sounds
- Direct original: https://opengameart.org/sites/default/files/bing1.wav
- License: **CC0 1.0**, https://creativecommons.org/publicdomain/zero/1.0/
- The source page permits optionally crediting Brian MacIntosh; credit is retained here.
- Retrieved 2026-11-12. Original retained as `Recorded/bing1.wav`.
- Original SHA-256: `d14941ce27e6b409e0cd941eaad7201d613070a4c3eb9c3627fec4cf3ae686e4`.
- This is a metallic impact recording, not a claim of a real bullet striking a range target.

## Processing and verification

`prepare_range_audio.py` decodes/downmixes/resamples with FFmpeg, removes leading
silence and low-frequency handling noise, applies short edit fades, and exports
mono 44.1 kHz, 16-bit PCM WAVs. Levels leave headroom for overlapping 700 RPM shots
and hit feedback at the current rifle's playback gains. It can also write an
offline all-hits burst preview; this is not a recording of Unity's final audio mix.

Run from the repository root:

```sh
python3 ArtSource/Audio/prepare_range_audio.py --out Assets/Audio --preview Logs/rifle-audio-preview.wav
```

Existing WAV paths and `.meta` GUIDs are preserved; scene rebuilding is unnecessary.
`make_sounds.py` is the old synthesized prototype generator, not the source for
these replacements. Impact, pickup, and the unused TargetHit_Low remain unchanged.

Numerical sample checks and Unity import checks do not establish that the sounds
are realistic, cleanly isolated, or pleasing. User listening/playtesting is still required.
