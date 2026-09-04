using UnityEngine;
using System.Collections;

namespace FightingGameBase
{
    /// <summary>
    /// 鞭（Whip）キャラクターの制御クラス
    /// リーチの長い whip lash（鞭の振り下ろし・しなり攻撃）動作を実装しています。
    /// </summary>
    public class WhipCharacter : CharacterBase
    {
        [Header("鞭キャラクター専用パーツ")]
        public Hitbox whipHitbox;
        public LineRenderer whipLineRenderer;
        
        private Coroutine currentWhipCoroutine;
        private bool isSwinging = false;
        
        // 振りかぶり中、または攻撃判定（ヒットボックス）がアクティブな間は「攻撃中」とみなす
        public override bool IsAttacking => isSwinging || (whipHitbox != null && whipHitbox.gameObject.activeInHierarchy);
        
        private TrailRenderer trailRenderer;
        private Animator anim;

        // =========================================================
        // 固有スキル「蛇鞭覚醒（Serpent Whip Awakening）」
        // スキルゲージが満タンの時にキーを押すと発動。
        // 攻撃距離と攻撃力が1.5倍になる（効果時間5秒）
        // =========================================================
        [Header("固有スキル: 蛇鞭覚醒")]
        [Tooltip("スキルゲージが満タンになるまでの時間（秒）")]
        public float skillChargeTime = 15f;

        [Tooltip("発動時の攻撃力・攻撃距離の倍率")]
        public float skillMultiplier = 1.5f;

        [Tooltip("スキル効果の持続時間（秒）")]
        public float skillDuration = 5f;

        [Tooltip("1試合でスキルを使える上限回数")]
        public int maxSkillUses = 1;

        /// <summary>
        /// 残りのスキル使用可能回数
        /// </summary>
        public int SkillUsesRemaining { get; private set; }

        /// <summary>
        /// 現在のスキルゲージ量（0.0～1.0）。外部UIから参照可能。
        /// </summary>
        public float SkillGauge { get; private set; } = 0f;

        /// <summary>
        /// スキルゲージが満タンかどうか
        /// </summary>
        public bool IsSkillReady => SkillGauge >= 1f && SkillUsesRemaining > 0;

        /// <summary>
        /// 固有スキルが発動中かどうか（外部からも参照可能）
        /// </summary>
        public bool IsSkillActive { get; private set; } = false;

        private int originalDamage;               // 元の攻撃力（復元用に保持）
        private float originalMaxScalingDistance;  // 元の距離補正最大距離
        private BoxCollider2D whipCollider;        // 鞭ヒットボックスのコライダー
        private Vector2 originalColliderSize;      // 元のコライダーサイズ
        private Vector2 originalColliderOffset;    // 元のコライダーオフセット
        private Vector3 originalHitboxLocalPos;    // 元のヒットボックスローカル位置
        private Coroutine skillCoroutine;          // スキル効果コルーチン
        private SpriteRenderer cachedSpriteRenderer; // スプライト参照キャッシュ
        private Color originalSpriteColor;         // 元のスプライト色

        private void Awake()
        {
            SkillUsesRemaining = maxSkillUses;

            trailRenderer = GetComponentInChildren<TrailRenderer>(true);
            anim = GetComponentInChildren<Animator>();

            // 鞭ヒットボックスの距離ベースダメージ補正を有効化
            // 先端（遠い位置）ほどダメージが大きく、根元（近い位置）ほどダメージが小さくなります
            if (whipHitbox != null)
            {
                whipHitbox.useDistanceScaling = true;
                whipHitbox.nearDamageMultiplier = 0.4f;   // 根元: 40%ダメージ（12 × 0.4 ≒ 5）
                whipHitbox.farDamageMultiplier = 1.8f;    // 先端: 180%ダメージ（12 × 1.8 ≒ 22）
                whipHitbox.maxScalingDistance = 4.0f;     // 最大有効距離（鞭の全長に合わせて調整）

                // 時間経過で攻撃力が低下する補正を有効化
                whipHitbox.useTimeScaling = true;
                whipHitbox.maxTimeScalingDuration = 0.6f;
                whipHitbox.startDamageMultiplier = 1.0f;  // 攻撃直後は100%のダメージ
                whipHitbox.endDamageMultiplier = 0.2f;    // 0.6秒後には20%まで威力が低下


                // 固有スキル用に元の値を記録
                originalDamage = whipHitbox.damage;
                originalMaxScalingDistance = whipHitbox.maxScalingDistance;
                whipCollider = whipHitbox.GetComponent<BoxCollider2D>();
                if (whipCollider != null)
                {
                    originalColliderSize = whipCollider.size;
                    originalColliderOffset = whipCollider.offset;
                }
                originalHitboxLocalPos = whipHitbox.transform.localPosition;
            }

            // スプライトレンダラーをキャッシュ
            Transform visuals = transform.Find("Visuals");
            cachedSpriteRenderer = visuals != null ? visuals.GetComponentInChildren<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
            if (cachedSpriteRenderer != null)
            {
                originalSpriteColor = cachedSpriteRenderer.color;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (isDead) return;
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

            // スキルゲージのチャージ（発動中でない時、かつ使用回数が残っている時のみ）
            if (!IsSkillActive && SkillGauge < 1f && SkillUsesRemaining > 0)
            {
                SkillGauge += Time.deltaTime / skillChargeTime;
                SkillGauge = Mathf.Clamp01(SkillGauge);
            }
        }

        /// <summary>
        /// 固有スキルの発動を試みます。
        /// PlayerInputControllerのスタンスキルキー（C / Numpad0）から呼び出されます。
        /// </summary>
        public override void AttackStun()
        {
            // ゲージが満タンでなければ発動しない
            if (!IsSkillReady)
            {
                Debug.Log($"スキルゲージが足りません！（{SkillGauge * 100f:F0}%）");
                return;
            }

            // すでに発動中なら重複発動しない
            if (IsSkillActive)
            {
                Debug.Log("蛇鞭覚醒はすでに発動中！");
                return;
            }

            if (isDead || isStunned) return;

            // 回数制限チェック
            if (SkillUsesRemaining <= 0)
            {
                Debug.Log("蛇鞭覚醒の使用回数上限に達しています！");
                return;
            }

            // ゲージを消費してスキル発動
            SkillGauge = 0f;
            SkillUsesRemaining--;
            skillCoroutine = StartCoroutine(UniqueSkillRoutine());
        }

        // =========================================================
        // 固有スキル発動・効果・解除処理
        // =========================================================

        /// <summary>
        /// 固有スキルの効果を一定時間適用し、終了後に元に戻すコルーチン。
        /// </summary>
        private IEnumerator UniqueSkillRoutine()
        {
            IsSkillActive = true;

            Debug.Log("【固有スキル発動】蛇鞭覚醒！ 攻撃力と攻撃距離が1.5倍に！（5秒間）");

            // --- 強化適用 ---
            ApplySkillBuffs();

            // --- 発動演出 ---
            yield return StartCoroutine(SkillActivationEffect());

            // --- 効果時間中の待機 ---
            yield return new WaitForSeconds(skillDuration);

            // --- 強化解除 ---
            RemoveSkillBuffs();

            Debug.Log("【固有スキル終了】蛇鞭覚醒の効果が切れた！");

            IsSkillActive = false;
            skillCoroutine = null;
        }

        /// <summary>
        /// スキルの強化効果を適用します。
        /// </summary>
        private void ApplySkillBuffs()
        {
            // --- 攻撃力を1.5倍に ---
            if (whipHitbox != null)
            {
                whipHitbox.damage = Mathf.RoundToInt(originalDamage * skillMultiplier);
                whipHitbox.maxScalingDistance = originalMaxScalingDistance * skillMultiplier;
            }

            // --- 攻撃距離（ヒットボックス）を1.5倍に ---
            if (whipCollider != null)
            {
                whipCollider.size = new Vector2(originalColliderSize.x * skillMultiplier, originalColliderSize.y);
                float addedWidth = (originalColliderSize.x * skillMultiplier - originalColliderSize.x) * 0.5f;
                whipCollider.offset = new Vector2(originalColliderOffset.x + addedWidth, originalColliderOffset.y);
            }

            // ヒットボックス位置を先端方向にずらす
            if (whipHitbox != null)
            {
                float posShift = (originalHitboxLocalPos.x * skillMultiplier) - originalHitboxLocalPos.x;
                whipHitbox.transform.localPosition = new Vector3(
                    originalHitboxLocalPos.x + posShift * 0.5f,
                    originalHitboxLocalPos.y,
                    originalHitboxLocalPos.z
                );
            }
        }

        /// <summary>
        /// スキルの強化効果を解除し、元のステータスに戻します。
        /// </summary>
        private void RemoveSkillBuffs()
        {
            // 攻撃力を元に戻す
            if (whipHitbox != null)
            {
                whipHitbox.damage = originalDamage;
                whipHitbox.maxScalingDistance = originalMaxScalingDistance;
            }

            // ヒットボックスサイズを元に戻す
            if (whipCollider != null)
            {
                whipCollider.size = originalColliderSize;
                whipCollider.offset = originalColliderOffset;
            }

            // ヒットボックス位置を元に戻す
            if (whipHitbox != null)
            {
                whipHitbox.transform.localPosition = originalHitboxLocalPos;
            }

            // スプライトの色を元に戻す
            if (cachedSpriteRenderer != null)
            {
                cachedSpriteRenderer.color = originalSpriteColor;
            }
        }

        /// <summary>
        /// 固有スキル発動時の視覚演出コルーチン。
        /// キャラクターが一瞬光り輝き、スプライトの色が変わります。
        /// </summary>
        private IEnumerator SkillActivationEffect()
        {
            if (cachedSpriteRenderer == null) yield break;

            // 発動の瞬間: 白く光るフラッシュ演出（3回点滅）
            for (int i = 0; i < 3; i++)
            {
                cachedSpriteRenderer.color = Color.white;
                yield return new WaitForSeconds(0.08f);
                cachedSpriteRenderer.color = originalSpriteColor;
                yield return new WaitForSeconds(0.08f);
            }

            // 覚醒状態の色に変更（赤みがかったピンク＝鞭の覚醒をイメージ）
            cachedSpriteRenderer.color = new Color(1.0f, 0.7f, 0.85f, 1f);

            // 一瞬だけ拡大してから戻す演出
            Transform visuals = transform.Find("Visuals");
            if (visuals != null)
            {
                Vector3 origScale = visuals.localScale;
                float t = 0f;
                float expandDur = 0.2f;
                while (t < expandDur)
                {
                    t += Time.deltaTime;
                    float rate = Mathf.Sin((t / expandDur) * Mathf.PI);
                    visuals.localScale = origScale * (1f + 0.3f * rate);
                    yield return null;
                }
                visuals.localScale = origScale;
            }
        }

        /// <summary>
        /// スタン等で状態リセットされた場合、スキルの効果も解除する
        /// </summary>
        public override void ResetActionStates()
        {
            base.ResetActionStates();

            if (IsSkillActive)
            {
                if (skillCoroutine != null)
                {
                    StopCoroutine(skillCoroutine);
                    skillCoroutine = null;
                }
                RemoveSkillBuffs();
                IsSkillActive = false;
            }
        }

        // =========================================================
        // 攻撃処理の上書き（鞭攻撃アニメーションの実装）
        // =========================================================

        /// <summary>
        /// 通常攻撃：素早い長リーチの鞭しなり振り下ろし (Whip Lash)
        /// </summary>
        public override void AttackNormal()
        {
            if (isDead || isStunned) return;

            // 攻撃判定を0.6秒間アクティブにする
            if (whipHitbox != null)
            {
                StartCoroutine(ActivateHitboxTemporarily(whipHitbox.gameObject, 0.6f));
            }

            if (anim != null && anim.runtimeAnimatorController != null)
            {
                anim.SetTrigger("AttackNormal");
            }
            else
            {
                Transform visuals = transform.Find("Visuals");
                if (visuals != null)
                {
                    if (currentWhipCoroutine != null)
                    {
                        StopCoroutine(currentWhipCoroutine);
                    }
                    currentWhipCoroutine = StartCoroutine(WhipLashAnimation(visuals, 0.6f));
                }
            }
        }

        /// <summary>
        /// 特殊攻撃：大上段からの強烈な鞭振り下ろしスラム (Overhead Whip Snap)
        /// </summary>
        public override void AttackSpecial()
        {
            if (isDead || isStunned) return;

            // 攻撃判定を0.6秒間アクティブにする
            if (whipHitbox != null)
            {
                StartCoroutine(ActivateHitboxTemporarily(whipHitbox.gameObject, 0.6f));
            }

            if (anim != null && anim.runtimeAnimatorController != null)
            {
                anim.SetTrigger("AttackSpecial");
            }
            else
            {
                Transform visuals = transform.Find("Visuals");
                if (visuals != null)
                {
                    if (currentWhipCoroutine != null)
                    {
                        StopCoroutine(currentWhipCoroutine);
                    }
                    currentWhipCoroutine = StartCoroutine(WhipOverheadSnapAnimation(visuals, 0.6f));
                }
            }
        }

        /// <summary>
        /// 必殺技：広範囲の3連連続鞭打ち連撃 (Whip Flurry)
        /// </summary>
        public override void AttackUltimate()
        {
            if (isDead || isStunned) return;

            // 攻撃判定を0.6秒間アクティブにする
            if (whipHitbox != null)
            {
                StartCoroutine(ActivateHitboxTemporarily(whipHitbox.gameObject, 0.6f));
            }

            if (anim != null && anim.runtimeAnimatorController != null)
            {
                anim.SetTrigger("AttackUltimate");
            }
            else
            {
                Transform visuals = transform.Find("Visuals");
                if (visuals != null)
                {
                    if (currentWhipCoroutine != null)
                    {
                        StopCoroutine(currentWhipCoroutine);
                    }
                    currentWhipCoroutine = StartCoroutine(WhipFlurryAnimation(visuals, 0.6f));
                }
            }
        }

        // =========================================================
        // アニメーションコルーチン群
        // =========================================================

        /// <summary>
        /// 1. 通常鞭振り下ろし (Whip Lash)
        /// 巻き上げ（引き足） → 前方へしなりながら鋭く振り下ろし → 巻き戻し
        /// </summary>
        private IEnumerator WhipLashAnimation(Transform visuals, float duration)
        {
            isSwinging = true;
            Vector3 originalPos = Vector3.zero;
            Quaternion originalRot = Quaternion.identity;
            float facing = 1f;

            // 各フェーズのトランスフォーム設定
            // 巻き上げため
            Quaternion pullbackRot = Quaternion.Euler(0, 0, 35f * facing);
            Vector3 pullbackPos = new Vector3(-0.25f * facing, 0.2f, 0f);

            // 前方への鋭い振り下ろしスナップ
            Quaternion snapRot = Quaternion.Euler(0, 0, -80f * facing);
            Vector3 snapPos = new Vector3(0.45f * facing, -0.15f, 0f);

            // しなり反動
            Quaternion reboundRot = Quaternion.Euler(0, 0, -65f * facing);
            Vector3 reboundPos = new Vector3(0.35f * facing, -0.05f, 0f);

            SetTrailActive(true);

            // Phase 1: 引き構え・巻き上げ (20%)
            float t = 0f;
            float phase1Time = duration * 0.2f;
            Quaternion startRot = visuals.localRotation;
            Vector3 startPos = visuals.localPosition;
            while (t < phase1Time)
            {
                t += Time.deltaTime;
                float rate = Mathf.SmoothStep(0f, 1f, t / phase1Time);
                visuals.localRotation = Quaternion.Lerp(startRot, pullbackRot, rate);
                visuals.localPosition = Vector3.Lerp(startPos, pullbackPos, rate);
                yield return null;
            }

            // Phase 2: 最速しなり振り下ろし（Whip Crack Impact） (25%)
            t = 0f;
            float phase2Time = duration * 0.25f;
            while (t < phase2Time)
            {
                t += Time.deltaTime;
                // 3乗加速（鞭の先端が超高速で振り下ろされる動き）
                float rate = Mathf.Pow(t / phase2Time, 3f);
                visuals.localRotation = Quaternion.Lerp(pullbackRot, snapRot, rate);
                visuals.localPosition = Vector3.Lerp(pullbackPos, snapPos, rate);
                yield return null;
            }
            visuals.localRotation = snapRot;
            visuals.localPosition = snapPos;

            // Phase 3: インパクトしなり保持 (15%)
            t = 0f;
            float phase3Time = duration * 0.15f;
            while (t < phase3Time)
            {
                t += Time.deltaTime;
                float rate = Mathf.Sin((t / phase3Time) * Mathf.PI * 0.5f);
                visuals.localRotation = Quaternion.Lerp(snapRot, reboundRot, rate);
                visuals.localPosition = Vector3.Lerp(snapPos, reboundPos, rate);
                yield return null;
            }

            SetTrailActive(false);

            // Phase 4: 手元への回収・復帰 (40%)
            t = 0f;
            float phase4Time = duration * 0.4f;
            while (t < phase4Time)
            {
                t += Time.deltaTime;
                float rate = Mathf.SmoothStep(0f, 1f, t / phase4Time);
                visuals.localRotation = Quaternion.Lerp(reboundRot, originalRot, rate);
                visuals.localPosition = Vector3.Lerp(reboundPos, originalPos, rate);
                yield return null;
            }

            visuals.localRotation = originalRot;
            visuals.localPosition = originalPos;
            isSwinging = false;
            currentWhipCoroutine = null;
        }

        /// <summary>
        /// 2. 特殊大振り下ろしスラム (Whip Overhead Snap)
        /// 高く跳びはねるように鞭を頭上へ振りかぶり、地面へ波打つように叩きつける
        /// </summary>
        private IEnumerator WhipOverheadSnapAnimation(Transform visuals, float duration)
        {
            isSwinging = true;
            Vector3 originalPos = Vector3.zero;
            Quaternion originalRot = Quaternion.identity;
            float facing = 1f;

            Quaternion highRot = Quaternion.Euler(0, 0, 50f * facing);
            Vector3 highPos = new Vector3(-0.3f * facing, 0.4f, 0f);

            Quaternion slamRot = Quaternion.Euler(0, 0, -100f * facing);
            Vector3 slamPos = new Vector3(0.6f * facing, -0.25f, 0f);

            SetTrailActive(true);

            // Phase 1: 大上段溜め (25%)
            float t = 0f;
            float p1 = duration * 0.25f;
            while (t < p1)
            {
                t += Time.deltaTime;
                float rate = Mathf.SmoothStep(0f, 1f, t / p1);
                visuals.localRotation = Quaternion.Lerp(originalRot, highRot, rate);
                visuals.localPosition = Vector3.Lerp(originalPos, highPos, rate);
                yield return null;
            }

            // Phase 2: 地面への強烈たたきつけ (20%)
            t = 0f;
            float p2 = duration * 0.2f;
            while (t < p2)
            {
                t += Time.deltaTime;
                float rate = Mathf.Pow(t / p2, 4f); // 激しい加速度
                visuals.localRotation = Quaternion.Lerp(highRot, slamRot, rate);
                visuals.localPosition = Vector3.Lerp(highPos, slamPos, rate);
                yield return null;
            }
            visuals.localRotation = slamRot;
            visuals.localPosition = slamPos;

            // 衝撃つぶれ演出
            Vector3 origScale = visuals.localScale;
            visuals.localScale = new Vector3(origScale.x * 1.2f, origScale.y * 0.8f, origScale.z);
            yield return new WaitForSeconds(duration * 0.15f);
            visuals.localScale = origScale;

            SetTrailActive(false);

            // Phase 3: 復帰 (40%)
            t = 0f;
            float p4 = duration * 0.4f;
            while (t < p4)
            {
                t += Time.deltaTime;
                float rate = Mathf.SmoothStep(0f, 1f, t / p4);
                visuals.localRotation = Quaternion.Lerp(slamRot, originalRot, rate);
                visuals.localPosition = Vector3.Lerp(slamPos, originalPos, rate);
                yield return null;
            }

            visuals.localRotation = originalRot;
            visuals.localPosition = originalPos;
            isSwinging = false;
            currentWhipCoroutine = null;
        }

        /// <summary>
        /// 3. 必殺鞭乱舞 (Whip Flurry)
        /// 下段払い → 逆袈裟 → 仕上げの縦一筆大振り下ろし
        /// </summary>
        private IEnumerator WhipFlurryAnimation(Transform visuals, float duration)
        {
            isSwinging = true;
            Vector3 originalPos = Vector3.zero;
            Quaternion originalRot = Quaternion.identity;
            float facing = 1f;

            float stepTime = duration / 3f;
            SetTrailActive(true);

            // 1段目: 低く払う振り下ろし
            yield return StartCoroutine(SubWhip(visuals, Quaternion.Euler(0, 0, 20f * facing), Quaternion.Euler(0, 0, -45f * facing), stepTime));

            // 2段目: 巻き上げからの逆カット
            yield return StartCoroutine(SubWhip(visuals, Quaternion.Euler(0, 0, -45f * facing), Quaternion.Euler(0, 0, 45f * facing), stepTime));

            // 3段目: 大上段フィニッシュ振り下ろし
            yield return StartCoroutine(SubWhip(visuals, Quaternion.Euler(0, 0, 60f * facing), Quaternion.Euler(0, 0, -90f * facing), stepTime));

            SetTrailActive(false);

            // 復帰
            float t = 0f;
            float returnTime = 0.15f;
            Quaternion currentRot = visuals.localRotation;
            Vector3 currentPos = visuals.localPosition;
            while (t < returnTime)
            {
                t += Time.deltaTime;
                float rate = t / returnTime;
                visuals.localRotation = Quaternion.Lerp(currentRot, originalRot, rate);
                visuals.localPosition = Vector3.Lerp(currentPos, originalPos, rate);
                yield return null;
            }

            visuals.localRotation = originalRot;
            visuals.localPosition = originalPos;
            isSwinging = false;
            currentWhipCoroutine = null;
        }

        private IEnumerator SubWhip(Transform visuals, Quaternion startRot, Quaternion endRot, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float rate = Mathf.SmoothStep(0f, 1f, t / dur);
                visuals.localRotation = Quaternion.Lerp(startRot, endRot, rate);
                yield return null;
            }
        }

        private void SetTrailActive(bool active)
        {
            if (trailRenderer != null)
            {
                trailRenderer.emitting = active;
            }
        }
    }
}
