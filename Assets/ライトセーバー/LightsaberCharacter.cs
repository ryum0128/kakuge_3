using UnityEngine;

namespace FightingGameBase
{
    public class LightsaberCharacter : CharacterBase
    {
        public Sprite spriteNormal;
        public Sprite spriteCounterStance;
        public Sprite spriteCounterFlash;
        public Sprite spriteCounterAttack;
        
        public Hitbox counterHitbox;
        
        private Coroutine currentSwingCoroutine;
        
        // 攻撃時に呼ばれる処理を上書きして、アニメーションを追加します
        public override void AttackNormal()
        {
            base.AttackNormal(); // 本来の攻撃判定を出す処理を実行

            // Visuals（キャラクターの見た目）を回転させて、剣を振り下ろす動きを作ります
            Transform visuals = transform.Find("Visuals");
            if (visuals != null)
            {
                if (currentSwingCoroutine != null)
                {
                    StopCoroutine(currentSwingCoroutine);
                }
                currentSwingCoroutine = StartCoroutine(SwingAnimation(visuals, 0.3f)); // 0.3秒かけてアニメーション
            }
        }

        private System.Collections.IEnumerator SwingAnimation(Transform visuals, float duration)
        {
            float elapsed = 0f;
            // 常に初期角度（0度）を基準にする
            Quaternion originalRot = Quaternion.identity;
            
            // 前に大きく傾けて「振り下ろし」を表現（Z軸に-60度回転）
            // キャラクターが左を向いている場合は逆に回転させる必要がある場合もありますが、基本はこれ
            Quaternion targetRot = originalRot * Quaternion.Euler(0, 0, -60f);
            
            // 現在の角度からスタート（連打対策）
            Quaternion startRot = visuals.localRotation;

            // 1. 素早く振り下ろす（全体の30%の時間）
            float swingDownTime = duration * 0.3f;
            while (elapsed < swingDownTime)
            {
                elapsed += Time.deltaTime;
                visuals.localRotation = Quaternion.Lerp(startRot, targetRot, elapsed / swingDownTime);
                yield return null;
            }
            
            visuals.localRotation = targetRot;
            
            // 2. 振り切った状態で少しだけキープ（全体の20%の時間）
            yield return new WaitForSeconds(duration * 0.2f);
            
            // 3. 元の姿勢に戻す（全体の50%の時間）
            elapsed = 0f;
            float returnTime = duration * 0.5f;
            while (elapsed < returnTime)
            {
                elapsed += Time.deltaTime;
                visuals.localRotation = Quaternion.Lerp(targetRot, originalRot, elapsed / returnTime);
                yield return null;
            }

            visuals.localRotation = originalRot;
            currentSwingCoroutine = null;
        }
    }
}
