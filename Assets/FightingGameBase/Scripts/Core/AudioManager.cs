using System.Collections;
using UnityEngine;

namespace FightingGameBase
{
    /// <summary>
    /// ゲーム全体の効果音（SE）やパリー音、前兆音、テレポート音を管理するオーディオマネージャー。
    /// 外部オーディオファイルに依存せず、動的に高品質なSEクリップを合成生成（プロシージャル生成）します。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private AudioSource seSource;
        private static AudioClip parryClip;
        private static AudioClip blockClip;
        private static AudioClip warningClip;
        private static AudioClip teleportClip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            EnsureInstance();
        }

        public static void EnsureInstance()
        {
            if (Instance == null)
            {
                AudioManager existing = FindAnyObjectByType<AudioManager>();
                if (existing != null)
                {
                    Instance = existing;
                }
                else
                {
                    GameObject go = new GameObject("AudioManager");
                    DontDestroyOnLoad(go);
                    Instance = go.AddComponent<AudioManager>();
                }
                Instance.InitAudio();
            }
        }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitAudio();
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void InitAudio()
        {
            if (seSource == null)
            {
                seSource = gameObject.GetComponent<AudioSource>();
                if (seSource == null)
                {
                    seSource = gameObject.AddComponent<AudioSource>();
                }
                seSource.playOnAwake = false;
                seSource.loop = false;
                seSource.spatialBlend = 0f; // 2Dサウンド
                seSource.volume = 1.0f;
            }

            if (parryClip == null) parryClip = GenerateParryClip();
            if (blockClip == null) blockClip = GenerateBlockClip();
            if (warningClip == null) warningClip = GenerateWarningClip();
            if (teleportClip == null) teleportClip = GenerateTeleportClip();
        }

        /// <summary>
        /// パリー成功時に呼び出され、爽快で澄み渡る金属パリー音（キィィィン！）を再生します。
        /// </summary>
        public static void PlayParrySound()
        {
            EnsureInstance();
            if (Instance != null && Instance.seSource != null)
            {
                if (parryClip == null) parryClip = GenerateParryClip();
                Instance.seSource.PlayOneShot(parryClip, 1.0f);
                Debug.Log("[AudioManager] ★ パリー成功音（キィィィン！）を再生しました ★");
            }
        }

        /// <summary>
        /// 通常ガード成功時の重い受け止め音（ガッ！）を再生します。
        /// </summary>
        public static void PlayBlockSound()
        {
            EnsureInstance();
            if (Instance != null && Instance.seSource != null)
            {
                if (blockClip == null) blockClip = GenerateBlockClip();
                Instance.seSource.PlayOneShot(blockClip, 0.75f);
            }
        }

        /// <summary>
        /// パリー対応攻撃や大技の振り下ろし前兆を知らせる高周波の閃光・予兆音（ピキィィン！）を再生します。
        /// </summary>
        public static void PlayWarningSound()
        {
            EnsureInstance();
            if (Instance != null && Instance.seSource != null)
            {
                if (warningClip == null) warningClip = GenerateWarningClip();
                Instance.seSource.PlayOneShot(warningClip, 0.85f);
            }
        }

        /// <summary>
        /// 忍者の黄色い閃光や瞬間移動時に鳴る、電光ワープ音（シュンッ・バチィン！）を再生します。
        /// </summary>
        public static void PlayTeleportSound()
        {
            EnsureInstance();
            if (Instance != null && Instance.seSource != null)
            {
                if (teleportClip == null) teleportClip = GenerateTeleportClip();
                Instance.seSource.PlayOneShot(teleportClip, 0.90f);
            }
        }

        /// <summary>
        /// 関（SEKIRO）やDeepwoken、ストリートファイター3rdを彷彿とさせるパリー音「キィィィン！」
        /// </summary>
        public static AudioClip GenerateParryClip()
        {
            int sampleRate = 44100;
            float duration = 0.65f;
            int sampleCount = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float phaseSweep = 0f;
            System.Random rand = new System.Random(777);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float noise = ((float)rand.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 110f) * 0.40f;
                float sweepFreq = 3800f * Mathf.Exp(-t * 70f) + 1860f;
                phaseSweep += 2f * Mathf.PI * sweepFreq / sampleRate;
                float sweepTone = Mathf.Sin(phaseSweep) * Mathf.Exp(-t * 28f) * 0.45f;
                float vib = 1f + 0.0035f * Mathf.Sin(2f * Mathf.PI * 12f * t);

                float m0 = Mathf.Sin(2f * Mathf.PI * 1860f * vib * t) * Mathf.Exp(-t * 6.2f) * 0.50f;
                float m1 = Mathf.Sin(2f * Mathf.PI * 2820f * vib * t) * Mathf.Exp(-t * 7.5f) * 0.38f;
                float m2 = Mathf.Sin(2f * Mathf.PI * 4480f * vib * t) * Mathf.Exp(-t * 12.0f) * 0.28f;
                float m3 = Mathf.Sin(2f * Mathf.PI * 6250f * t) * Mathf.Exp(-t * 18.0f) * 0.20f;
                float m4 = Mathf.Sin(2f * Mathf.PI * 930f * t) * Mathf.Exp(-t * 9.0f) * 0.28f;
                float m5 = Mathf.Sin(2f * Mathf.PI * 8200f * t) * Mathf.Exp(-t * 25.0f) * 0.15f;

                float sample = noise + sweepTone + m0 + m1 + m2 + m3 + m4 + m5;
                float attack = Mathf.Clamp01(t / 0.0012f);
                samples[i] = sample * attack;
            }

            Normalize(samples, 0.95f);
            AudioClip clip = AudioClip.Create("SE_Parry", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip GenerateBlockClip()
        {
            int sampleRate = 44100;
            float duration = 0.22f;
            int sampleCount = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            System.Random rand = new System.Random(333);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float noise = ((float)rand.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 45f) * 0.5f;
                float low = Mathf.Sin(2f * Mathf.PI * 260f * t) * Mathf.Exp(-t * 22f) * 0.5f;
                float mid = Mathf.Sin(2f * Mathf.PI * 580f * t) * Mathf.Exp(-t * 30f) * 0.35f;

                samples[i] = (noise + low + mid) * Mathf.Clamp01(t / 0.001f);
            }

            Normalize(samples, 0.90f);
            AudioClip clip = AudioClip.Create("SE_Block", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip GenerateWarningClip()
        {
            int sampleRate = 44100;
            float duration = 0.28f;
            int sampleCount = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float freq = Mathf.Lerp(2200f, 3900f, Mathf.Sqrt(t / duration));
                phase += 2f * Mathf.PI * freq / sampleRate;

                float tone1 = Mathf.Sin(phase) * Mathf.Exp(-t * 14f) * 0.45f;
                float tone2 = Mathf.Sin(phase * 1.5f) * Mathf.Exp(-t * 18f) * 0.25f;
                float tone3 = Mathf.Sin(phase * 2.0f) * Mathf.Exp(-t * 24f) * 0.15f;

                samples[i] = (tone1 + tone2 + tone3) * Mathf.Clamp01(t / 0.002f);
            }

            Normalize(samples, 0.85f);
            AudioClip clip = AudioClip.Create("SE_WarningTell", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// 黄色い閃光・瞬間移動の電光風ワープ音（シュンッ！）
        /// </summary>
        public static AudioClip GenerateTeleportClip()
        {
            int sampleRate = 44100;
            float duration = 0.22f;
            int sampleCount = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float phase = 0f;
            System.Random rand = new System.Random(888);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                // 急速ピッチダウンの電光スウィープ（3600Hz -> 650Hz）
                float sweepFreq = 3600f * Mathf.Exp(-t * 22f) + 650f;
                phase += 2f * Mathf.PI * sweepFreq / sampleRate;

                float tone = Mathf.Sin(phase) * Mathf.Exp(-t * 16f) * 0.55f;
                float zapNoise = ((float)rand.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 40f) * 0.35f;
                float sub = Mathf.Sin(phase * 0.5f) * Mathf.Exp(-t * 12f) * 0.30f;

                samples[i] = (tone + zapNoise + sub) * Mathf.Clamp01(t / 0.001f);
            }

            Normalize(samples, 0.90f);
            AudioClip clip = AudioClip.Create("SE_Teleport", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static void Normalize(float[] samples, float targetMax)
        {
            float max = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float abs = Mathf.Abs(samples[i]);
                if (abs > max) max = abs;
            }
            if (max > 0.001f)
            {
                float gain = targetMax / max;
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] *= gain;
                }
            }
        }
    }
}
