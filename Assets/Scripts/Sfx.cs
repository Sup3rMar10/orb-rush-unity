using UnityEngine;

namespace OrbRush
{
    /// <summary>Sound effects synthesised at start-up (sine/saw beeps), so there are no audio files either.</summary>
    public sealed class Sfx
    {
        private const int SampleRate = 44100;

        private readonly AudioSource source;
        private readonly AudioClip collect;
        private readonly AudioClip hit;
        private readonly AudioClip levelUp;
        private readonly AudioClip gameOver;

        public Sfx(GameObject host)
        {
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.5f;

            collect = Tone("collect", new[] { 880f, 1320f }, 0.08f, false);
            hit = Tone("hit", new[] { 180f, 110f }, 0.25f, true);
            levelUp = Tone("levelup", new[] { 523f, 659f, 784f, 1046f }, 0.09f, false);
            gameOver = Tone("gameover", new[] { 392f, 330f, 262f, 196f }, 0.2f, true);
        }

        public void Collect() { Play(collect); }
        public void Hit() { Play(hit); }
        public void LevelUp() { Play(levelUp); }
        public void GameOver() { Play(gameOver); }

        private void Play(AudioClip clip)
        {
            if (clip != null) source.PlayOneShot(clip);
        }

        /// <summary>One note per frequency, each noteSeconds long, with a quick fade so notes do not click.</summary>
        private static AudioClip Tone(string name, float[] freqs, float noteSeconds, bool saw)
        {
            int perNote = Mathf.RoundToInt(noteSeconds * SampleRate);
            var data = new float[perNote * freqs.Length];
            for (int n = 0; n < freqs.Length; n++)
            {
                double phase = 0.0;
                double step = freqs[n] / SampleRate;
                for (int i = 0; i < perNote; i++)
                {
                    phase += step;
                    phase -= System.Math.Floor(phase);
                    float wave = saw ? (float)(phase * 2.0 - 1.0) : Mathf.Sin((float)(phase * 2.0 * System.Math.PI));
                    float t = (float)i / perNote;
                    float env = Mathf.Min(1f, t * 20f) * (1f - t);
                    data[n * perNote + i] = wave * env * 0.4f;
                }
            }
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
