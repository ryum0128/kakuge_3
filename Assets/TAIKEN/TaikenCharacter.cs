using UnityEngine;
using System.Collections;

namespace FightingGameBase
{
    // =================================================================================
    // 【TaikenCharacter（大剣キャラクター用のカスタム挙動）】
    // CharacterBase を継承し、大剣に特化した攻撃モーションと判定制御を行います。
    // 通常攻撃時に、見た目のオブジェクトをプログラムで素早く振り下ろし、ゆっくり戻します。
    // =================================================================================
    public class TaikenCharacter : CharacterBase
    {
        [Header("大剣の攻撃モーション設定")]
        [Tooltip("振り下ろし開始時の回転角度")]
        public float swingStartAngle = 45f;
        [Tooltip("振り下ろしきった時の回転角度")]
        public float swingEndAngle = -90f;
        [Tooltip("振り下ろしにかかる時間（秒）")]
        public float swingDuration = 0.12f;
        [Tooltip("元の位置に戻る時間（秒）")]
        public float recoverDuration = 0.28f;

        private Transform visualsTransform;
        private bool isSwinging = false;

        void Start()
        {
            // 親クラス (CharacterBase) の初期設定を実行
            base.Start();
            
            // 診断用ログ: アタッチされているオブジェクト名と子オブジェクトをコンソールに出力します
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (Transform child in transform)
            {
                sb.Append(child.name).Append(" (Active: ").Append(child.gameObject.activeSelf).Append("), ");
            }
            string childrenList = sb.Length > 0 ? sb.ToString().TrimEnd(' ', ',') : "(子オブジェクトなし)";
            Debug.Log($"<b>[TaikenCharacter 診断]</b> アタッチ先オブジェクト: <color=yellow>'{gameObject.name}'</color> | 子オブジェクト一覧: <color=cyan>{childrenList}</color>");

            // 子要素から HoverEffect コンポーネントを探して、見た目（Visuals）のオブジェクトを特定します
            HoverEffect hover = GetComponentInChildren<HoverEffect>(true);
            if (hover != null)
            {
                visualsTransform = hover.transform;
            }
            else
            {
                // バックアップとして名前で検索します
                visualsTransform = transform.Find("Visuals");
            }

            if (visualsTransform == null)
            {
                Debug.LogError($"<b>TaikenCharacter エラー</b>: 大剣の見た目オブジェクト（Visuals）が見つかりませんでした！\n" +
                               $"・アタッチされているオブジェクト: '{gameObject.name}'\n" +
                               $"・検知された子オブジェクト: {childrenList}\n" +
                               $"・対策: 生成メニューを再実行し、新しく生成されたプレハブを配置し直してください。");
            }
        }

        // 通常攻撃（AttackNormal）を大剣仕様に上書き（override）します！
        public override void AttackNormal()
        {
            if (isDead || isSwinging) return;

            Debug.Log("大剣・通常攻撃（振り下ろし）発動！");
            
            // アニメーターがセットされていれば、トリガーをセット
            if (animator != null)
            {
                animator.SetTrigger("AttackNormal");
            }

            // 通常より少し広い範囲を攻撃するため、コルーチンで大剣専用のタイミングで判定を出します
            Hitbox hitbox = GetComponentInChildren<Hitbox>(true);
            if (hitbox != null)
            {
                // 振り下ろし動作（0.12秒）の少し後まで当たり判定が残るように0.22秒間に設定
                StartCoroutine(ActivateHitboxTemporarily(hitbox.gameObject, 0.22f));
            }

            // プログラム制御による振り下ろしモーションを開始
            StartCoroutine(SwingRoutine());
        }

        // 振り下ろしを表現するコルーチン
        private IEnumerator SwingRoutine()
        {
            isSwinging = true;
            Debug.Log("大剣アニメーション: 振り下ろし開始！");
            float elapsed = 0f;

            // 1. 素早く振り下ろす (swingStartAngle -> swingEndAngle)
            while (elapsed < swingDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / swingDuration;
                
                // 加速しながら振り下ろすイージング効果
                t = t * t; 
                
                float angle = Mathf.Lerp(swingStartAngle, swingEndAngle, t);
                if (visualsTransform != null)
                {
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                }
                yield return null;
            }

            // 完全に振り下ろした角度に固定
            if (visualsTransform != null)
            {
                visualsTransform.localRotation = Quaternion.Euler(0, 0, swingEndAngle);
            }
            
            // 振り切った状態で一瞬（0.04秒）静止させて重厚感を出す
            yield return new WaitForSeconds(0.04f);

            Debug.Log("大剣アニメーション: 元の位置に戻し始めます。");

            // 2. ゆっくりと元の角度（0度）に戻す
            elapsed = 0f;
            while (elapsed < recoverDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / recoverDuration;
                
                // 減速しながら滑らかに戻るイージング効果
                t = Mathf.Sin(t * Mathf.PI * 0.5f);
                
                float angle = Mathf.Lerp(swingEndAngle, 0f, t);
                if (visualsTransform != null)
                {
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                }
                yield return null;
            }

            // 完全に元の状態にリセット
            if (visualsTransform != null)
            {
                visualsTransform.localRotation = Quaternion.identity;
            }
            Debug.Log("大剣アニメーション: 元の位置に戻りました。");
            isSwinging = false;
        }

        // =========================================================
        // 特殊攻撃: 溜め斬り（ChargedSlash）
        // Kキーを押すと大剣を後ろに大きく引いて溜め、
        // 一気に超高速で前方に振り抜くパワー全開の一撃！
        // =========================================================

        [Header("溜め斬り設定")]
        [Tooltip("溜めフェーズの時間（秒）")]
        public float chargeWindupDuration = 0.7f;

        [Tooltip("溜め時の引き角度（大きく引くほど予備動作が大きく見える）")]
        public float chargeWindupAngle = 120f;

        [Tooltip("解放フェーズの時間（秒）- 短いほど速く激しく見える")]
        public float chargeReleaseDuration = 0.06f;

        [Tooltip("解放後の振り抜き角度")]
        public float chargeReleaseAngle = -130f;

        [Tooltip("溜め斬りのダメージ（通常攻撃より大きく設定）")]
        public int chargeSlashDamage = 30;

        // 特殊攻撃のオーバーライド
        public override void AttackSpecial()
        {
            if (isDead || isSwinging) return;
            Debug.Log("大剣・特殊攻撃（溜め斬り）発動！！");
            StartCoroutine(ChargedSwingRoutine());
        }

        // 溜め斬りの3フェーズコルーチン
        private IEnumerator ChargedSwingRoutine()
        {
            isSwinging = true;

            // ---------------------------------------------------
            // フェーズ1: 溜め（チャージ）
            // ゆっくり剣を後方に引いてパワーをため込む
            // ---------------------------------------------------
            Debug.Log("溜め斬り: [フェーズ1] 溜め開始...");
            float elapsed = 0f;

            while (elapsed < chargeWindupDuration)
            {
                elapsed += Time.deltaTime;
                // 最初はゆっくり、後半につれて少し速くなる（EaseIn）
                float t = Mathf.Pow(elapsed / chargeWindupDuration, 0.6f);
                float angle = Mathf.Lerp(0f, chargeWindupAngle, t);
                if (visualsTransform != null)
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }

            // 引き切った位置で一瞬止める（ため切り感）
            if (visualsTransform != null)
                visualsTransform.localRotation = Quaternion.Euler(0, 0, chargeWindupAngle);
            yield return new WaitForSeconds(0.05f);

            // ---------------------------------------------------
            // フェーズ2: 解放（リリース）
            // 超高速で一気に前方へ振り抜く！当たり判定もここで発生！
            // ---------------------------------------------------
            Debug.Log("溜め斬り: [フェーズ2] 解放！！");

            // 溜め斬り専用の超広範囲ヒットボックスを取得して発動
            Hitbox hitbox = GetComponentInChildren<Hitbox>(true);
            if (hitbox != null)
            {
                // ダメージを一時的に溜め斬り用の高ダメージに変更して発動
                int originalDamage = hitbox.damage;
                hitbox.damage = chargeSlashDamage;
                StartCoroutine(ActivateHitboxTemporarily(hitbox.gameObject, chargeReleaseDuration + 0.08f));
                // ダメージを元に戻すコルーチンを続けて実行
                StartCoroutine(RestoreDamage(hitbox, originalDamage, chargeReleaseDuration + 0.1f));
            }

            elapsed = 0f;
            while (elapsed < chargeReleaseDuration)
            {
                elapsed += Time.deltaTime;
                // 急激に加速して叩き込む（EaseIn - t^4）
                float t = Mathf.Pow(elapsed / chargeReleaseDuration, 4f);
                float angle = Mathf.Lerp(chargeWindupAngle, chargeReleaseAngle, t);
                if (visualsTransform != null)
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }

            // 振り抜き位置で少し止める（爽快感）
            if (visualsTransform != null)
                visualsTransform.localRotation = Quaternion.Euler(0, 0, chargeReleaseAngle);
            yield return new WaitForSeconds(0.08f);

            // ---------------------------------------------------
            // フェーズ3: 戻し（リカバリー）
            // 滑らかに元の位置に戻る
            // ---------------------------------------------------
            Debug.Log("溜め斬り: [フェーズ3] リカバリー中...");
            elapsed = 0f;
            float recoverTime = recoverDuration * 1.3f; // 通常より少しゆっくり戻す
            while (elapsed < recoverTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Sin((elapsed / recoverTime) * Mathf.PI * 0.5f);
                float angle = Mathf.Lerp(chargeReleaseAngle, 0f, t);
                if (visualsTransform != null)
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }

            if (visualsTransform != null)
                visualsTransform.localRotation = Quaternion.identity;

            Debug.Log("溜め斬り: 完了！");
            isSwinging = false;
        }

        // ダメージ値を元に戻すコルーチン
        private IEnumerator RestoreDamage(Hitbox hitbox, int originalDamage, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (hitbox != null)
                hitbox.damage = originalDamage;
        }

        // =========================================================
        // ブロック・パリーシステム
        // Hキー押下でガード姿勢。押した瞬間0.25秒はパリー受付窓。
        // パリー成功: ダメージ0 + バウンスアニメ
        // 通常ブロック: ダメージ0（ガード中）
        // =========================================================

        [Header("ブロック・パリー設定")]
        [Tooltip("ブロック時の剣の角度（横構えの角度）")]
        public float blockAngle = 85f;

        [Tooltip("ブロック姿勢に移行する時間（秒）")]
        public float blockEnterDuration = 0.08f;

        [Tooltip("パリー受付窓の長さ（秒）- この間に攻撃を受けるとパリー成功！")]
        public float parryWindow = 0.25f;

        private bool isBlocking = false;
        private bool isParrying = false;   // パリー受付窓が開いているか
        private Coroutine blockCoroutine;

        // ブロック開始（Hキーを押した瞬間）
        public override void StartBlock()
        {
            if (isDead || isSwinging) return;

            isBlocking = true;
            isParrying = true;
            Debug.Log("ブロック開始！パリー受付窓オープン！");

            // 既存のブロックコルーチンがあれば止める
            if (blockCoroutine != null) StopCoroutine(blockCoroutine);
            blockCoroutine = StartCoroutine(BlockEnterRoutine());

            // パリー窓を時間で閉じる
            StartCoroutine(CloseParryWindow());
        }

        // ブロック解除（Hキーを離した瞬間）
        public override void StopBlock()
        {
            if (!isBlocking) return;

            isBlocking = false;
            isParrying = false;
            Debug.Log("ブロック解除。");

            if (blockCoroutine != null) StopCoroutine(blockCoroutine);
            blockCoroutine = StartCoroutine(BlockExitRoutine());
        }

        // ダメージ処理をオーバーライド: ブロック中はノーダメ
        public override void TakeDamage(int damage)
        {
            if (isDead) return;

            if (isBlocking)
            {
                if (isParrying)
                {
                    // ★ パリー成功！ダメージ0 + バウンスアニメ
                    Debug.Log("★パリー成功！ノーダメージ！");
                    StartCoroutine(ParrySuccessRoutine());
                }
                else
                {
                    // 通常ブロック: ダメージ0
                    Debug.Log("ブロック成功！ダメージを防いだ！");
                    StartCoroutine(BlockHitRoutine());
                }
                return; // ダメージを通さない
            }

            // ブロックしていなければ通常のダメージ処理
            base.TakeDamage(damage);
        }

        // --- ブロック・パリーのアニメーションコルーチン ---

        // ブロック姿勢に素早く移行する
        private IEnumerator BlockEnterRoutine()
        {
            float elapsed = 0f;
            float startAngle = visualsTransform != null
                ? visualsTransform.localRotation.eulerAngles.z
                : 0f;
            // eulerAngles は 0-360 で返ってくるので -180 変換
            if (startAngle > 180f) startAngle -= 360f;

            while (elapsed < blockEnterDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / blockEnterDuration;
                // 素早く移行（EaseOut）
                t = 1f - Mathf.Pow(1f - t, 3f);
                float angle = Mathf.Lerp(startAngle, blockAngle, t);
                if (visualsTransform != null)
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }

            if (visualsTransform != null)
                visualsTransform.localRotation = Quaternion.Euler(0, 0, blockAngle);
        }

        // ブロック姿勢から元の位置に戻る
        private IEnumerator BlockExitRoutine()
        {
            float elapsed = 0f;
            float exitDuration = 0.15f;

            while (elapsed < exitDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Sin((elapsed / exitDuration) * Mathf.PI * 0.5f);
                float angle = Mathf.Lerp(blockAngle, 0f, t);
                if (visualsTransform != null)
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }

            if (visualsTransform != null)
                visualsTransform.localRotation = Quaternion.identity;
        }

        // パリー受付窓を一定時間後に閉じる
        private IEnumerator CloseParryWindow()
        {
            yield return new WaitForSeconds(parryWindow);
            if (isBlocking)
            {
                isParrying = false;
                Debug.Log("パリー窓クローズ（通常ブロック状態へ）");
            }
        }

        // パリー成功時のバウンスアニメーション
        private IEnumerator ParrySuccessRoutine()
        {
            // ① 一瞬ぐっと後ろに弾かれる（衝撃）
            float elapsed = 0f;
            float bounceDuration = 0.06f;
            while (elapsed < bounceDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / bounceDuration;
                float angle = Mathf.Lerp(blockAngle, blockAngle - 40f, t);
                if (visualsTransform != null)
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }

            // ② キラッと逆方向に切り返す（パリー反撃の気配）
            elapsed = 0f;
            float flashDuration = 0.1f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / flashDuration;
                float angle = Mathf.Lerp(blockAngle - 40f, blockAngle + 25f, t * t);
                if (visualsTransform != null)
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }

            // ③ ゆっくりガード姿勢に戻る（まだブロック中）
            elapsed = 0f;
            float returnDuration = 0.12f;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Sin((elapsed / returnDuration) * Mathf.PI * 0.5f);
                float angle = Mathf.Lerp(blockAngle + 25f, blockAngle, t);
                if (visualsTransform != null)
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }

            if (visualsTransform != null)
                visualsTransform.localRotation = Quaternion.Euler(0, 0, blockAngle);
        }

        // 通常ブロック被弾時の小さな揺れアニメーション
        private IEnumerator BlockHitRoutine()
        {
            float elapsed = 0f;
            float shakeDuration = 0.08f;
            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / shakeDuration;
                // 小さく揺れる
                float angle = blockAngle - Mathf.Sin(t * Mathf.PI) * 15f;
                if (visualsTransform != null)
                    visualsTransform.localRotation = Quaternion.Euler(0, 0, angle);
                yield return null;
            }

            if (visualsTransform != null)
                visualsTransform.localRotation = Quaternion.Euler(0, 0, blockAngle);
        }
    }
}
