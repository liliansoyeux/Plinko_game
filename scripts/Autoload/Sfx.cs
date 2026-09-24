using Godot;
using System;
using System.Collections.Generic;

namespace Plinko;

public enum Sound
{
    Peg,
    Drop,
    Slot,
    BadSlot,
    Miss,
    Jackpot,
    Chest,
    CursedChest,
    LevelUp,
    Malus,
    PalierClear,
    GameOver,
    Click,
    Hover,
    Pick,
    Boss,
    Portal,
    Place,
}

// Every sound is synthesized at startup (no audio files in the project): short additive
// bell/blip tones with exponential envelopes, played through a small round-robin pool.
public partial class Sfx : Node
{
    private const int MixRate = 44100;
    private const int PoolSize = 20;

    private static Sfx _instance;
    private readonly Dictionary<Sound, AudioStreamWav> _streams = new();
    private readonly List<AudioStreamPlayer> _players = new();
    private int _next;
    private readonly Dictionary<int, ulong> _lastPlayedMs = new();
    private readonly Random _noise = new(1234);
    private AudioStreamPlayer _music;

    public override void _Ready()
    {
        _instance = this;
        ProcessMode = ProcessModeEnum.Always;

        for (int i = 0; i < PoolSize; i++)
        {
            var player = new AudioStreamPlayer { Bus = "Master" };
            AddChild(player);
            _players.Add(player);
        }

        Build();
        SetMuted(SaveData.Muted);

        _music = new AudioStreamPlayer { Stream = BuildMusic(), VolumeDb = -30f, Bus = "Master" };
        AddChild(_music);
        _music.Play();
        CreateTween().TweenProperty(_music, "volume_db", -13f, 2.5f);
    }

    public static bool Muted => AudioServer.IsBusMute(0);

    public static void SetMuted(bool muted)
    {
        AudioServer.SetBusMute(0, muted);
    }

    public static void Play(Sound sound, float pitch = 1f, float volumeDb = 0f)
    {
        if (_instance == null || !_instance._streams.TryGetValue(sound, out var stream))
        {
            return;
        }

        if (sound is Sound.Peg or Sound.Slot or Sound.Portal)
        {
            // Dozens of balls at once would otherwise turn into white noise.
            ulong now = Time.GetTicksMsec();
            int index = (int)sound;
            ulong gap = sound == Sound.Peg ? 28UL : 45UL;
            if (now - _instance._lastPlayedMs.GetValueOrDefault(index) < gap)
            {
                return;
            }
            _instance._lastPlayedMs[index] = now;
        }

        var player = _instance._players[_instance._next];
        _instance._next = (_instance._next + 1) % _instance._players.Count;
        player.Stream = stream;
        player.PitchScale = Mathf.Clamp(pitch, 0.25f, 4f);
        player.VolumeDb = volumeDb;
        player.Play();
    }

    private void Build()
    {
        _streams[Sound.Peg] = Synth(0.12f, t => Bell(t, 2350f, 38f) * 0.55f + Bell(t, 2350f * 2.76f, 60f) * 0.18f);
        _streams[Sound.Drop] = Synth(0.12f, t => Sine(t, Sweep(t, 0.1f, 380f, 220f)) * Env(t, 28f) * 0.6f);
        _streams[Sound.Slot] = Synth(0.3f, t =>
            (Sine(t, Sweep(t, 0.05f, 520f, 780f)) + 0.35f * Sine(t, Sweep(t, 0.05f, 1040f, 1560f))) * Env(t, 11f) * 0.5f);
        _streams[Sound.BadSlot] = Synth(0.3f, t =>
            Triangle(t, Sweep(t, 0.25f, 330f, 160f)) * Env(t, 9f) * 0.55f);
        _streams[Sound.Miss] = Synth(0.35f, t =>
            (Triangle(t, Sweep(t, 0.3f, 220f, 90f)) * 0.6f + Noise() * 0.15f) * Env(t, 8f) * 0.6f);
        _streams[Sound.Jackpot] = Arpeggio(new[] { 72, 76, 79, 84, 88 }, 0.07f, 0.9f, 0.5f, sparkle: true);
        _streams[Sound.Chest] = Synth(0.7f, t =>
        {
            float sweep = Sine(t, Sweep(t, 0.35f, 600f, 1800f)) * Env(t, 5f) * 0.3f;
            float twinkle = Bell(t % 0.09f, 2600f + 400f * MathF.Floor(t / 0.09f), 30f) * 0.25f * Env(t, 3f);
            return sweep + twinkle;
        });
        _streams[Sound.CursedChest] = Synth(0.8f, t =>
            (Saw(t, Sweep(t, 0.7f, 220f, 110f)) * 0.25f + Saw(t, Sweep(t, 0.7f, 233f, 116f)) * 0.25f) * Env(t, 3.5f) * 1f);
        _streams[Sound.LevelUp] = Arpeggio(new[] { 67, 71, 74, 79 }, 0.06f, 0.55f, 0.45f, sparkle: false);
        _streams[Sound.Malus] = Synth(0.6f, t =>
            (Square(t, Sweep(t, 0.5f, 180f, 90f)) * 0.3f + Square(t, Sweep(t, 0.5f, 190f, 95f)) * 0.2f) * Env(t, 5f) * 1.2f);
        _streams[Sound.PalierClear] = Fanfare();
        _streams[Sound.GameOver] = Arpeggio(new[] { 67, 63, 60, 55 }, 0.22f, 1.5f, 0.5f, sparkle: false, decay: 3.5f);
        _streams[Sound.Click] = Synth(0.06f, t => Sine(t, 1400f) * Env(t, 70f) * 0.45f);
        _streams[Sound.Hover] = Synth(0.04f, t => Sine(t, 2100f) * Env(t, 110f) * 0.2f);
        _streams[Sound.Pick] = Arpeggio(new[] { 76, 83, 88 }, 0.05f, 0.5f, 0.45f, sparkle: true);
        _streams[Sound.Portal] = Synth(0.9f, t =>
        {
            float wobble = 1f + 0.03f * MathF.Sin(MathF.Tau * 7f * t);
            float sweep = Sine(t, Sweep(t, 0.8f, 300f, 1400f) * wobble) * 0.3f + Sine(t, Sweep(t, 0.8f, 450f, 2100f)) * 0.15f;
            return (sweep + Bell(t % 0.07f, 3000f, 50f) * 0.1f) * MathF.Min(1f, t * 10f) * Env(t, 3f) * 0.9f;
        });
        _streams[Sound.Place] = Synth(0.25f, t =>
            (Sine(t, Sweep(t, 0.08f, 220f, 110f)) * 0.7f + Bell(t, 1500f, 40f) * 0.25f) * Env(t, 14f) * 0.8f);
        _streams[Sound.Boss] = Synth(1.2f, t =>
            (Saw(t, 73.4f) * 0.3f + Saw(t, 73.4f * 1.5f) * 0.2f + Square(t, 36.7f) * 0.2f) * MathF.Min(1f, t * 8f) * Env(t, 2.2f) * 1.1f);
    }

    // A 20-second lounge loop: electric-piano comping over a ii-V-I-vi progression, a soft
    // walking bass and a brushed hi-hat. Rendered into a circular buffer so ringing notes
    // wrap around the loop point instead of being cut off.
    private AudioStreamWav BuildMusic()
    {
        const float bpm = 96f;
        const float beat = 60f / bpm;
        const int bars = 8;
        float length = bars * 4 * beat;
        int n = (int)(length * MixRate);
        var buffer = new float[n];

        int[][] chords =
        {
            new[] { 50, 57, 60, 64, 65 }, // Dm9
            new[] { 43, 53, 57, 59, 64 }, // G13
            new[] { 48, 55, 59, 62, 64 }, // Cmaj9
            new[] { 45, 55, 58, 61, 64 }, // A7b9
            new[] { 50, 57, 60, 64, 65 }, // Dm9
            new[] { 43, 53, 57, 59, 64 }, // G13
            new[] { 52, 55, 59, 62, 67 }, // Em7
            new[] { 45, 55, 57, 61, 64 }, // A7
        };

        for (int bar = 0; bar < bars; bar++)
        {
            var chord = chords[bar];
            float barStart = bar * 4 * beat;

            // Bass: root on 1, fifth on 3, a passing note on the "and" of 4.
            AddNote(buffer, barStart, Midi(chord[0] - 12), 1.6f * beat, Bass, 0.34f);
            AddNote(buffer, barStart + 2 * beat, Midi(chord[0] - 5), 1.6f * beat, Bass, 0.28f);
            AddNote(buffer, barStart + 3.5f * beat, Midi(chords[(bar + 1) % bars][0] - 11), 0.5f * beat, Bass, 0.22f);

            // Rhodes comping: on 1 and on the swung "and" of 2.
            foreach (float hit in new[] { 0f, 1.66f })
            {
                for (int i = 1; i < chord.Length; i++)
                {
                    float velocity = hit == 0f ? 0.11f : 0.075f;
                    AddNote(buffer, barStart + hit * beat + i * 0.012f, Midi(chord[i]), 2.4f * beat, Rhodes, velocity);
                }
            }

            // Brushed hats on the swung off-beats.
            for (int b = 0; b < 4; b++)
            {
                AddNoise(buffer, barStart + (b + 0.66f) * beat, 0.07f, 0.045f);
            }
        }

        var data = new byte[n * 2];
        for (int i = 0; i < n; i++)
        {
            short v = (short)(Math.Clamp(MathF.Tanh(buffer[i] * 1.2f), -1f, 1f) * 30000f);
            data[i * 2] = (byte)(v & 0xFF);
            data[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }

        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = MixRate,
            Stereo = false,
            Data = data,
            LoopMode = AudioStreamWav.LoopModeEnum.Forward,
            LoopBegin = 0,
            LoopEnd = n,
        };
    }

    private static float Rhodes(float t, float f) =>
        (MathF.Sin(MathF.Tau * f * t) + 0.25f * MathF.Sin(MathF.Tau * 2f * f * t) * MathF.Exp(-t * 6f)
         + 0.08f * MathF.Sin(MathF.Tau * 4.02f * f * t) * MathF.Exp(-t * 14f))
        * MathF.Exp(-t * 1.3f) * (1f + 0.12f * MathF.Sin(MathF.Tau * 5f * t));

    private static float Bass(float t, float f) =>
        (MathF.Sin(MathF.Tau * f * t) + 0.3f * MathF.Sin(MathF.Tau * 2f * f * t)) * MathF.Exp(-t * 2.2f);

    private static void AddNote(float[] buffer, float start, float freq, float duration, Func<float, float, float> voice, float gain)
    {
        int s0 = (int)(start * MixRate);
        int len = (int)(duration * MixRate);
        int release = MixRate / 20;
        for (int k = 0; k < len; k++)
        {
            float t = k / (float)MixRate;
            float env = MathF.Min(1f, k / 60f) * MathF.Min(1f, (len - k) / (float)release);
            buffer[(s0 + k) % buffer.Length] += voice(t, freq) * env * gain;
        }
    }

    private void AddNoise(float[] buffer, float start, float duration, float gain)
    {
        int s0 = (int)(start * MixRate);
        int len = (int)(duration * MixRate);
        float previous = 0f;
        for (int k = 0; k < len; k++)
        {
            // Crude high-pass (difference of white noise) for a brushy, airy tick.
            float white = Noise();
            float sample = white - previous;
            previous = white;
            buffer[(s0 + k) % buffer.Length] += sample * MathF.Exp(-k / (float)MixRate * 45f) * gain;
        }
    }

    // Debug: write every synthesized sound to disk so it can be inspected offline.
    public static void DumpAll(string directory)
    {
        DirAccess.MakeDirRecursiveAbsolute(directory);
        foreach (var (sound, stream) in _instance._streams)
        {
            stream.SaveToWav($"{directory}/{sound}.wav");
        }
        ((AudioStreamWav)_instance._music.Stream).SaveToWav($"{directory}/Music.wav");
    }

    private AudioStreamWav Fanfare()
    {
        var notes = new[] { 60, 64, 67, 72 };
        return Synth(1.4f, t =>
        {
            float s = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float start = i * 0.09f;
                if (t < start) continue;
                float lt = t - start;
                float f = Midi(notes[i]);
                float sustain = i == notes.Length - 1 ? 2.2f : 5f;
                s += (Sine(lt, f) * 0.5f + Square(lt, f) * 0.12f + Sine(lt, f * 2f) * 0.15f) * Env(lt, sustain);
            }
            return s * 0.32f;
        });
    }

    private AudioStreamWav Arpeggio(int[] notes, float step, float duration, float gain, bool sparkle, float decay = 7f)
    {
        return Synth(duration, t =>
        {
            float s = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float start = i * step;
                if (t < start) continue;
                float lt = t - start;
                float f = Midi(notes[i]);
                s += (Sine(lt, f) * 0.6f + Sine(lt, f * 2f) * 0.2f + Sine(lt, f * 3.01f) * 0.08f) * Env(lt, decay);
            }
            if (sparkle)
            {
                s += Bell(t % 0.06f, 3200f, 45f) * 0.12f * Env(t, 4f);
            }
            return s * gain;
        });
    }

    private static float Midi(int note) => 440f * MathF.Pow(2f, (note - 69) / 12f);
    private static float Env(float t, float k) => MathF.Exp(-t * k) * MathF.Min(1f, t * 400f);
    private static float Sine(float t, float f) => MathF.Sin(MathF.Tau * f * t);
    private static float Bell(float t, float f, float k) => Sine(t, f) * Env(t, k);
    private static float Square(float t, float f) => MathF.Sign(MathF.Sin(MathF.Tau * f * t)) * 0.6f;
    private static float Saw(float t, float f) => 2f * (t * f - MathF.Floor(0.5f + t * f));
    private static float Triangle(float t, float f) => 2f * MathF.Abs(Saw(t, f)) - 1f;
    private float Noise() => (float)_noise.NextDouble() * 2f - 1f;

    // Frequency glide from f0 to f1 over the first `duration` seconds, phase-continuous
    // enough for short blips (integrated linearly rather than sampled per-t).
    private static float Sweep(float t, float duration, float f0, float f1)
    {
        float u = MathF.Min(t, duration);
        float integral = f0 * u + (f1 - f0) * u * u / (2f * duration);
        if (t > duration) integral += f1 * (t - duration);
        return integral / MathF.Max(t, 1e-6f);
    }

    private static AudioStreamWav Synth(float duration, Func<float, float> generator)
    {
        int count = (int)(duration * MixRate);
        var data = new byte[count * 2];
        int fadeOut = Math.Min(count / 4, MixRate / 50);
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)MixRate;
            float s = generator(t);
            if (i > count - fadeOut)
            {
                s *= (count - i) / (float)fadeOut;
            }
            s = Math.Clamp(s, -1f, 1f);
            short v = (short)(s * 32000f);
            data[i * 2] = (byte)(v & 0xFF);
            data[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }

        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = MixRate,
            Stereo = false,
            Data = data,
        };
    }
}
