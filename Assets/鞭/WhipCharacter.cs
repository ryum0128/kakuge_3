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
        public override bool IsAttacking => isSwinging;
        private TrailRenderer trailRenderer;
        private Animator anim;

        private void Awake()
        {
            trailRenderer = GetComponentInChildren<TrailRenderer>(true);
            anim = GetComponentInChildren<Animator>();
        }

        // =========================================================
        // 攻撃処理の上書き（鞭攻撃アニメーションの実装）
        // =========================================================

        /// <summary>
        /// 通常攻撃：素早い長リーチの鞭しなり振り下ろし (Whip Lash)
        /// </summary>
        public override void AttackNormal()
        {
            base.AttackNormal();

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
                    currentWhipCoroutine = StartCoroutine(WhipLashAnimation(visuals, 0.35f));
                }
            }
        }

        /// <summary>
        /// 特殊攻撃：大上段からの強烈な鞭振り下ろしスラム (Overhead Whip Snap)
        /// </summary>
        public override void AttackSpecial()
        {
            base.AttackSpecial();

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
                    currentWhipCoroutine = StartCoroutine(WhipOverheadSnapAnimation(visuals, 0.48f));
                }
            }
        }

        /// <summary>
        /// 必殺技：広範囲の3連連続鞭打ち連撃 (Whip Flurry)
        /// </summary>
        public override void AttackUltimate()
        {
            base.AttackUltimate();

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
                    currentWhipCoroutine = StartCoroutine(WhipFlurryAnimation(visuals, 0.65f));
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
