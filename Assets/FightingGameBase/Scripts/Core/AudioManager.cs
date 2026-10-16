using UnityEngine;

namespace FightingGameBase
{
    // =================================================================================
    // 【AudioManager】
    // パリー成功音（SE_Parry: 鋼鉄の澄んだキィィィン！）およびガード音の自動再生・管理
    // シーン開始時に自動初期化され、2Dオーディオでクリア＆大迫力に再生されます。
    // =================================================================================
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private AudioSource seSource;
        private static AudioClip parryClip;
        private static AudioClip blockClip;

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
                seSource.spatialBlend = 0f; // 2Dサウンド（カメラ距離に減衰されず全開で響く）
                seSource.volume = 1.0f;
            }

            if (parryClip == null)
            {
                parryClip = GenerateParryClip();
            }

            if (blockClip == null)
            {
                blockClip = GenerateBlockClip();
            }
        }

        /// <summary>
        /// パリー成功時に呼び出され、爽快で澄み渡る金属パリー音（キィィィン！）を再生します。
        /// </summary>
        public static void PlayParrySound()
        {
            EnsureInstance();
            if (Instance != null && Instance.seSource != null)
            {
                if (parryClip == null)
                {
                    parryClip = GenerateParryClip();
                }
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
                if (blockClip == null)
                {
                    blockClip = GenerateBlockClip();
                }
                Instance.seSource.PlayOneShot(blockClip, 0.75f);
            }
        }

        /// <summary>
        /// 関（SEKIRO）やDeepwoken、ストリートファイター3rdを彷彿とさせる、
        /// 鋼鉄の刃が激突して美しい残響を放つ極上のパリー音「キィィィン！」を合成生成します。
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

                // 1. 初動の衝撃衝突クラッシュ（0.00〜0.04秒の金属衝突ノイズ）
                float noise = ((float)rand.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 110f) * 0.40f;

                // 2. 衝突瞬間の高周波ピッチスウィープ（金属の弾き）
                float sweepFreq = 3800f * Mathf.Exp(-t * 70f) + 1860f;
                phaseSweep += 2f * Mathf.PI * sweepFreq / sampleRate;
                float sweepTone = Mathf.Sin(phaseSweep) * Mathf.Exp(-t * 28f) * 0.45f;

                // 3. 刀身が美しく鳴り響く微妙なピッチビブラート（12Hz）
                float vib = 1f + 0.0035f * Mathf.Sin(2f * Mathf.PI * 12f * t);

                // 4. 金属刃物固有の非調和モード倍音群（澄んだ高音の金属音）
                float m0 = Mathf.Sin(2f * Mathf.PI * 1860f * vib * t) * Mathf.Exp(-t * 6.2f) * 0.50f; // 基本澄音 (1860Hz)
                float m1 = Mathf.Sin(2f * Mathf.PI * 2820f * vib * t) * Mathf.Exp(-t * 7.5f) * 0.38f; // 5度上共鳴 (2820Hz)
                float m2 = Mathf.Sin(2f * Mathf.PI * 4480f * vib * t) * Mathf.Exp(-t * 12.0f) * 0.28f; // 金属ベル音 (4480Hz)
                float m3 = Mathf.Sin(2f * Mathf.PI * 6250f * t) * Mathf.Exp(-t * 18.0f) * 0.20f; // 高域シャープネス
                float m4 = Mathf.Sin(2f * Mathf.PI * 930f * t) * Mathf.Exp(-t * 9.0f) * 0.28f;  // 鍔鳴りの低域 (930Hz)
                float m5 = Mathf.Sin(2f * Mathf.PI * 8200f * t) * Mathf.Exp(-t * 25.0f) * 0.15f; // きらめき成分

                float sample = noise + sweepTone + m0 + m1 + m2 + m3 + m4 + m5;

                // 5. アタック立ち上がり（クリックノイズ防止）
                float attack = Mathf.Clamp01(t / 0.0012f);
                samples[i] = sample * attack;
            }

            // ピークノーマライズ（歪まない最大音量 0.95 に調整）
            float max = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float abs = Mathf.Abs(samples[i]);
                if (abs > max) max = abs;
            }
            if (max > 0.001f)
            {
                float gain = 0.95f / max;
                for (int i = 0; i < sampleCount; i++)
                {
                    samples[i] *= gain;
                }
            }

            AudioClip clip = AudioClip.Create("SE_Parry", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// 通常ガード成功時の重厚な打撃受止め音「ガッ！」を合成生成します。
        /// </summary>
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

                float sample = (noise + low + mid) * Mathf.Clamp01(t / 0.001f);
                samples[i] = sample;
            }

            float max = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float abs = Mathf.Abs(samples[i]);
                if (abs > max) max = abs;
            }
            if (max > 0.001f)
            {
                float gain = 0.90f / max;
                for (int i = 0; i < sampleCount; i++)
                {
                    samples[i] *= gain;
                }
            }

            AudioClip clip = AudioClip.Create("SE_Block", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
