using UnityEngine;
using System.Collections;

namespace FightingGameBase
{
    /// <summary>
    /// ライトセーバーキャラクター制御クラス
    /// 左右向き（1P/2P）に自動対応し、前方向への強烈な「振り下ろし」アニメーションを実行します。
    /// </summary>
    public class LightsaberCharacter : CharacterBase
    {
        public Sprite spriteNormal;
        public Sprite spriteCounterStance;
        public Sprite spriteCounterFlash;
        public Sprite spriteCounterAttack;
        
        public Hitbox counterHitbox;
        
        private Coroutine currentSwingCoroutine;
        private bool isSwinging = false;
        public override bool IsAttacking => isSwinging;
        public override bool CanUseStunSkill => true; // ライトセーバーのみスタンスキル使用可能
        private bool isCounterStance = false;
        private SpriteRenderer spriteRenderer;
        private TrailRenderer trailRenderer;
        private Animator anim;

        private void Awake()
        {
            // 残像用 TrailRenderer や Animator が子オブジェクト（Visuals 等）にあれば自動取得
            trailRenderer = GetComponentInChildren<TrailRenderer>(true);
            anim = GetComponentInChildren<Animator>();
        }

        protected override void Start()
        {
            base.Start();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
        
        // =========================================================
        // 攻撃処理の上書き（Unity用アニメーション制御）
        // =========================================================

        /// <summary>
        /// 通常攻撃：スピーディーな標準振り下ろし（オーバースイング）
        /// </summary>
        public override void AttackNormal()
        {
            base.AttackNormal(); // 本来の攻撃判定・SE発生処理

            // 1. Animator（.animアニメーションクリップ）がセットされている場合はアニメーター優先で再生
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                anim.SetTrigger("AttackNormal");
            }
            else
            {
                // 2. Animatorがセットされていない場合はプログラム（コルーチン）でスムーズに振り下ろしを表現
                Transform visuals = transform.Find("Visuals");
                if (visuals != null)
                {
                    if (currentSwingCoroutine != null)
                    {
                        StopCoroutine(currentSwingCoroutine);
                    }
                    currentSwingCoroutine = StartCoroutine(OverheadSlashAnimation(visuals, 0.32f));
                }
            }
        }

        /// <summary>
        /// 特殊攻撃：強力な両手持ち重撃スラム（大振り下ろし）
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
                    if (currentSwingCoroutine != null)
                    {
                        StopCoroutine(currentSwingCoroutine);
                    }
                    currentSwingCoroutine = StartCoroutine(HeavySlamAnimation(visuals, 0.45f));
                }
            }
        }

        /// <summary>
        /// 必殺技：3段連続振り下ろし斬撃
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
                    if (currentSwingCoroutine != null)
                    {
                        StopCoroutine(currentSwingCoroutine);
                    }
                    currentSwingCoroutine = StartCoroutine(TripleSlashAnimation(visuals, 0.6f));
                }
            }
        }

        // =========================================================
        // アニメーション用コルーチン群（振り下ろしモーション群）
        // =========================================================

        /// <summary>
        /// 1. 標準振り下ろしアニメーション (Overhead Slash)
        /// 振りかぶり(予備動作) → 一気に一閃 → 反動 → 滑らかな復帰
        /// </summary>
        private IEnumerator OverheadSlashAnimation(Transform visuals, float duration)
        {
            isSwinging = true;
            Vector3 originalPos = Vector3.zero;
            Quaternion originalRot = Quaternion.identity;

            // 左右どちらを向いていても正しく前方に振り下ろすための向き係数
            float facing = 1f;

            // 各フェーズの目標設定
            // フェーズ1: 振りかぶり (後ろ上方にためる)
            Quaternion windupRot = Quaternion.Euler(0, 0, 25f * facing);
            Vector3 windupPos = new Vector3(-0.1f * facing, 0.15f, 0f);

            // フェーズ2: 振り下ろし (前方へ大きく一気に振り切る)
            Quaternion slashRot = Quaternion.Euler(0, 0, -75f * facing);
            Vector3 slashPos = new Vector3(0.25f * facing, -0.1f, 0f);

            // フェーズ3: 反動・しなり
            Quaternion reboundRot = Quaternion.Euler(0, 0, -65f * facing);
            Vector3 reboundPos = new Vector3(0.2f * facing, -0.05f, 0f);

            // トレイル（残像）開始
            SetTrailActive(true);

            // --- Phase 1: 振りかぶり (予備動作 15%の時間) ---
            float t = 0f;
            float windupTime = duration * 0.15f;
            Quaternion startRot = visuals.localRotation;
            Vector3 startPos = visuals.localPosition;
            while (t < windupTime)
            {
                t += Time.deltaTime;
                float rate = Mathf.SmoothStep(0f, 1f, t / windupTime);
                visuals.localRotation = Quaternion.Lerp(startRot, windupRot, rate);
                visuals.localPosition = Vector3.Lerp(startPos, windupPos, rate);
                yield return null;
            }

            // --- Phase 2: 高速振り下ろし (一閃 25%の時間 - イージングで加速) ---
            t = 0f;
            float slashTime = duration * 0.25f;
            while (t < slashTime)
            {
                t += Time.deltaTime;
                // イーズイン（加速度的に速く振る）
                float rate = (t / slashTime) * (t / slashTime);
                visuals.localRotation = Quaternion.Lerp(windupRot, slashRot, rate);
                visuals.localPosition = Vector3.Lerp(windupPos, slashPos, rate);
                yield return null;
            }
            visuals.localRotation = slashRot;
            visuals.localPosition = slashPos;

            // --- Phase 3: 反動とキープ (15%の時間) ---
            t = 0f;
            float reboundTime = duration * 0.15f;
            while (t < reboundTime)
            {
                t += Time.deltaTime;
                float rate = Mathf.Sin((t / reboundTime) * Mathf.PI * 0.5f);
                visuals.localRotation = Quaternion.Lerp(slashRot, reboundRot, rate);
                visuals.localPosition = Vector3.Lerp(slashPos, reboundPos, rate);
                yield return null;
            }

            // トレイル停止
            SetTrailActive(false);

            // --- Phase 4: 元の姿勢に復帰 (45%の時間) ---
            t = 0f;
            float returnTime = duration * 0.45f;
            while (t < returnTime)
            {
                t += Time.deltaTime;
                float rate = Mathf.SmoothStep(0f, 1f, t / returnTime);
                visuals.localRotation = Quaternion.Lerp(reboundRot, originalRot, rate);
                visuals.localPosition = Vector3.Lerp(reboundPos, originalPos, rate);
                yield return null;
            }

            visuals.localRotation = originalRot;
            visuals.localPosition = originalPos;
            isSwinging = false;
            currentSwingCoroutine = null;
        }

        /// <summary>
        /// 2. 重撃振り下ろしスラム (Heavy Slam)
        /// 大きく振りかぶり、地面を割るような大振り下ろし
        /// </summary>
        private IEnumerator HeavySlamAnimation(Transform visuals, float duration)
        {
            isSwinging = true;
            Vector3 originalPos = Vector3.zero;
            Quaternion originalRot = Quaternion.identity;
            float facing = 1f;

            // 大きなため動作
            Quaternion windupRot = Quaternion.Euler(0, 0, 45f * facing);
            Vector3 windupPos = new Vector3(-0.2f * facing, 0.3f, 0f);

            // 地面へのたたきつけ
            Quaternion slamRot = Quaternion.Euler(0, 0, -95f * facing);
            Vector3 slamPos = new Vector3(0.4f * facing, -0.2f, 0f);

            SetTrailActive(true);

            // 1. ため (25%)
            float t = 0f;
            float phase1 = duration * 0.25f;
            while (t < phase1)
            {
                t += Time.deltaTime;
                float rate = Mathf.SmoothStep(0f, 1f, t / phase1);
                visuals.localRotation = Quaternion.Lerp(originalRot, windupRot, rate);
                visuals.localPosition = Vector3.Lerp(originalPos, windupPos, rate);
                yield return null;
            }

            // 2. 激しく振り下ろし (20%)
            t = 0f;
            float phase2 = duration * 0.2f;
            while (t < phase2)
            {
                t += Time.deltaTime;
                float rate = Mathf.Pow(t / phase2, 3f); // 超加速
                visuals.localRotation = Quaternion.Lerp(windupRot, slamRot, rate);
                visuals.localPosition = Vector3.Lerp(windupPos, slamPos, rate);
                yield return null;
            }
            visuals.localRotation = slamRot;
            visuals.localPosition = slamPos;

            // 3. インパクト持続＆つぶれ演出 (15%)
            Vector3 originalScale = visuals.localScale;
            visuals.localScale = new Vector3(originalScale.x * 1.15f, originalScale.y * 0.85f, originalScale.z);
            yield return new WaitForSeconds(duration * 0.15f);
            visuals.localScale = originalScale;

            SetTrailActive(false);

            // 4. 復帰 (40%)
            t = 0f;
            float phase4 = duration * 0.4f;
            while (t < phase4)
            {
                t += Time.deltaTime;
                float rate = Mathf.SmoothStep(0f, 1f, t / phase4);
                visuals.localRotation = Quaternion.Lerp(slamRot, originalRot, rate);
                visuals.localPosition = Vector3.Lerp(slamPos, originalPos, rate);
                yield return null;
            }

            visuals.localRotation = originalRot;
            visuals.localPosition = originalPos;
            isSwinging = false;
            currentSwingCoroutine = null;
        }

        /// <summary>
        /// 3. 3段連続振り下ろし斬撃 (Triple Slash)
        /// 袈裟斬り → 逆袈裟斬り → フィニッシュ縦振り下ろし
        /// </summary>
        private IEnumerator TripleSlashAnimation(Transform visuals, float duration)
        {
            isSwinging = true;
            Vector3 originalPos = Vector3.zero;
            Quaternion originalRot = Quaternion.identity;
            float facing = 1f;

            float stepDuration = duration / 3.0f;
            SetTrailActive(true);

            // 1段目: 右上から左下への袈裟斬り
            yield return StartCoroutine(SubSlash(visuals, Quaternion.Euler(0, 0, 30f * facing), Quaternion.Euler(0, 0, -60f * facing), stepDuration));
            
            // 2段目: 左下からのすくい上げ〜逆振り下ろし
            yield return StartCoroutine(SubSlash(visuals, Quaternion.Euler(0, 0, -60f * facing), Quaternion.Euler(0, 0, 40f * facing), stepDuration));
            
            // 3段目: 大上段からの強力フルトップ振り下ろし
            yield return StartCoroutine(SubSlash(visuals, Quaternion.Euler(0, 0, 50f * facing), Quaternion.Euler(0, 0, -85f * facing), stepDuration));

            SetTrailActive(false);

            // 復帰
            float t = 0f;
            float returnTime = 0.12f;
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
            currentSwingCoroutine = null;
        }

        private IEnumerator SubSlash(Transform visuals, Quaternion startAngle, Quaternion endAngle, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float rate = Mathf.SmoothStep(0f, 1f, t / dur);
                visuals.localRotation = Quaternion.Lerp(startAngle, endAngle, rate);
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


        public override void ResetActionStates()
        {
            base.ResetActionStates();
            isSwinging = false;
            isCounterStance = false;
            if (spriteRenderer != null && spriteNormal != null)
            {
                spriteRenderer.sprite = spriteNormal;
            }
            if (counterHitbox != null)
            {
                counterHitbox.gameObject.SetActive(false);
            }
        }

        public override void StartBlock()
        {
            if (isDead || isSwinging || isStunned) return;
            StartCoroutine(CounterStanceRoutine());
        }

        public override void StopBlock()
        {
            isCounterStance = false;
        }

        private IEnumerator CounterStanceRoutine()
        {
            isCounterStance = true;
            isSwinging = true;

            if (spriteRenderer != null && spriteCounterStance != null)
            {
                spriteRenderer.sprite = spriteCounterStance;
            }

            float duration = 0.4f;
            float elapsed = 0f;
            while (elapsed < duration && isCounterStance)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (isCounterStance)
            {
                isCounterStance = false;
                if (spriteRenderer != null && spriteNormal != null)
                {
                    spriteRenderer.sprite = spriteNormal;
                }
                isSwinging = false;
            }
        }

        private void TriggerCounterAttack(int incomingDamage, Hitbox attackerHitbox)
        {
            isCounterStance = false;
            StopAllCoroutines();
            StartCoroutine(CounterAttackRoutine(incomingDamage, attackerHitbox));
        }

        private IEnumerator CounterAttackRoutine(int incomingDamage, Hitbox attackerHitbox)
        {
            isSwinging = true;

            if (spriteRenderer != null && spriteCounterFlash != null)
            {
                spriteRenderer.sprite = spriteCounterFlash;
            }
            yield return new WaitForSeconds(0.1f);

            if (spriteRenderer != null && spriteCounterAttack != null)
            {
                spriteRenderer.sprite = spriteCounterAttack;
            }

            int counterDamage = incomingDamage * 2;
            if (counterHitbox != null)
            {
                counterHitbox.damage = counterDamage;
                counterHitbox.owner = this;
                counterHitbox.ownerPlayerID = playerID;
                counterHitbox.gameObject.SetActive(true);
            }

            float facingDir = transform.localScale.x;
            Vector2 startPos = transform.position;
            float strikeDuration = 0.25f;
            float elapsed = 0f;
            while (elapsed < strikeDuration)
            {
                elapsed += Time.deltaTime;
                rb.linearVelocity = new Vector2(facingDir * 6f, rb.linearVelocity.y);
                yield return null;
            }
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            if (counterHitbox != null)
            {
                counterHitbox.gameObject.SetActive(false);
            }

            yield return new WaitForSeconds(0.15f);

            if (spriteRenderer != null && spriteNormal != null)
            {
                spriteRenderer.sprite = spriteNormal;
            }

            isSwinging = false;
        }

        public override void TakeDamage(int damage, Hitbox attackerHitbox = null)
        {
            if (isCounterStance)
            {
                TriggerCounterAttack(damage, attackerHitbox);
                return;
            }

            // If the attack is a projectile and HP is 90% or higher, negate it!
            if (attackerHitbox != null && attackerHitbox.isProjectile)
            {
                float healthPercent = (float)currentHP / maxHP;
                if (healthPercent >= 0.9f)
                {
                    Debug.Log("Lightsaber negated projectile damage (HP >= 90%).");
                    return; // Ignore damage entirely!
                }
            }

            base.TakeDamage(damage, attackerHitbox);
        }
    }
}

