using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Sound pass 2026-11-22: cuts a short seamless loop out of a long ambience recording, so a 40-second file does not
    // sit decompressed in memory for a sound that only has to keep going. The segment's tail is crossfaded (equal
    // power) into its head, so the cut loops without a click. A cut with no crossfade is a one-shot trimmed of its
    // silent lead-in instead (length 0 = to the end), with a few milliseconds of fade-in so the cut does not click.
    // Re-running overwrites the same WAV from the same source.
    public static class AudioLoopCutter
    {
        public readonly struct Cut
        {
            public readonly string Source;
            public readonly string Output;
            public readonly float Start;
            public readonly float Length;
            public readonly float Crossfade;

            public Cut(string source, string output, float start, float length, float crossfade)
            {
                Source = source;
                Output = output;
                Start = start;
                Length = length;
                Crossfade = crossfade;
            }
        }

        // The originals stay outside the project (tmp/sound-candidates); only the cut loops are imported.
        public const string SourceRoot = "../tmp/sound-candidates";

        public static readonly Cut[] Cuts =
        {
            new($"{SourceRoot}/soundreality-fire-sound-334130.mp3", "Assets/Audio/SFX/SFX_Trap_Fire_Loop.wav", 4f, 8f, 1f),
            new($"{SourceRoot}/freesound_community-earth-rumble-6953.mp3", "Assets/Audio/SFX/SFX_Ambush_Move_Loop.wav", 4f, 6f, 1f),
            new($"{SourceRoot}/lordsonny_two-debris-break-253779.mp3", "Assets/Audio/SFX/SFX_Obstacle_Break.wav", 0.49f, 0f, 0f),
        };

        private const float TrimFadeInSeconds = 0.005f;

        [MenuItem("Trickal Fan Game/Audio/Cut Ambience Loops")]
        public static void CutAll()
        {
            foreach (Cut cut in Cuts) CutOne(cut);
            AssetDatabase.Refresh();
            Debug.Log($"Cut {Cuts.Length} ambience loops.");
        }

        // Logs each source's loudness per second, to choose where a cut starts.
        public static void LogEnvelopes() => LogEnvelopes(1f, float.PositiveInfinity);

        // Logs the first two seconds in 10ms steps, to find where a one-shot's sound begins.
        public static void LogLeadIns() => LogEnvelopes(0.01f, 2f);

        private static void LogEnvelopes(float window, float span)
        {
            foreach (Cut cut in Cuts)
            {
                (float[] data, int channels, int rate) = Load(cut.Source);
                StringBuilder line = new($"Envelope {Path.GetFileName(cut.Source)} ({data.Length / channels / (float)rate:0.0}s, {window}s steps):");
                int step = Mathf.Max(1, Mathf.RoundToInt(window * rate)) * channels;
                int limit = (int)Math.Min(data.Length, span * rate * channels);
                for (int from = 0; from < limit; from += step)
                {
                    double sum = 0;
                    int to = Math.Min(data.Length, from + step);
                    for (int i = from; i < to; i++) sum += data[i] * data[i];
                    line.Append($" {Math.Sqrt(sum / Math.Max(1, to - from)):0.000}");
                }

                Debug.Log(line.ToString());
            }
        }

        private static (float[] Data, int Channels, int Rate) Load(string projectRelative)
        {
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", projectRelative));
            if (!File.Exists(full)) throw new InvalidOperationException($"Loop source is missing: {full}");
            // AudioClips import only from inside Assets, so the source is copied in for the read and removed again.
            string temp = $"Assets/__LoopCutterSource{Path.GetExtension(full)}";
            File.Copy(full, temp, true);
            try
            {
                AssetDatabase.ImportAsset(temp, ImportAssetOptions.ForceSynchronousImport);
                if (AssetImporter.GetAtPath(temp) is AudioImporter importer)
                {
                    AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    importer.defaultSampleSettings = settings;
                    importer.SaveAndReimport();
                }

                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(temp);
                if (clip == null || !clip.LoadAudioData())
                    throw new InvalidOperationException($"Could not decode {full}.");
                float[] data = new float[clip.samples * clip.channels];
                if (!clip.GetData(data, 0)) throw new InvalidOperationException($"Could not read samples of {full}.");
                return (data, clip.channels, clip.frequency);
            }
            finally
            {
                AssetDatabase.DeleteAsset(temp);
            }
        }

        private static void CutOne(Cut cut)
        {
            (float[] data, int channels, int rate) = Load(cut.Source);
            int start = Mathf.RoundToInt(cut.Start * rate);
            int length = Mathf.RoundToInt(cut.Length * rate);
            int fade = Mathf.RoundToInt(cut.Crossfade * rate);
            int frames = data.Length / channels;
            if (fade <= 0)
            {
                if (length <= 0) length = frames - start;
                if (start < 0 || length <= 0 || start + length > frames)
                    throw new InvalidOperationException($"{cut.Output}: the trim does not fit in {frames / (float)rate:0.0}s.");
                int fadeIn = Mathf.Max(1, Mathf.RoundToInt(TrimFadeInSeconds * rate));
                float[] trimmed = new float[length * channels];
                for (int frame = 0; frame < length; frame++)
                for (int channel = 0; channel < channels; channel++)
                    trimmed[frame * channels + channel] = data[(start + frame) * channels + channel] *
                                                          Mathf.Clamp01(frame / (float)fadeIn);
                WriteWav(cut.Output, trimmed, channels, rate);
                return;
            }

            if (fade > length || start + length + fade > frames)
                throw new InvalidOperationException($"{cut.Output}: the cut does not fit in {frames / (float)rate:0.0}s.");

            // out[i] = seg[i] past the crossfade; within it, the segment's head fades in under the audio that follows
            // its end, so the last frame of the loop runs straight on into its first.
            float[] output = new float[length * channels];
            for (int frame = 0; frame < length; frame++)
            {
                float t = frame < fade ? frame / (float)fade : 1f;
                float fadeIn = Mathf.Sin(t * Mathf.PI * 0.5f), fadeOut = Mathf.Cos(t * Mathf.PI * 0.5f);
                for (int channel = 0; channel < channels; channel++)
                {
                    float head = data[(start + frame) * channels + channel];
                    float tail = frame < fade ? data[(start + length + frame) * channels + channel] : 0f;
                    output[frame * channels + channel] = head * fadeIn + tail * fadeOut;
                }
            }

            WriteWav(cut.Output, output, channels, rate);
        }

        private static void WriteWav(string path, float[] samples, int channels, int rate)
        {
            using FileStream stream = File.Create(path);
            using BinaryWriter writer = new(stream);
            int bytes = samples.Length * 2;
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + bytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(rate);
            writer.Write(rate * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(bytes);
            foreach (float sample in samples)
                writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(sample * 32767f), short.MinValue, short.MaxValue));
        }
    }
}
