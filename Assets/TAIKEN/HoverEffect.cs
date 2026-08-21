using UnityEngine;

namespace FightingGameBase
{
    // =================================================================================
    // 【HoverEffect（ホバー効果）】
    // このスクリプトは、オブジェクトを上下にふわふわと浮遊させるためのものです。
    // 大剣キャラクターの見た目（Visuals）を宙に浮いているように見せるために使用します。
    // =================================================================================
    public class HoverEffect : MonoBehaviour
    {
        [Header("ホバー設定")]
        [Tooltip("浮遊する速度（往復する速さ）")]
        public float hoverSpeed = 3f;

        [Tooltip("浮遊する幅（上下の移動量）")]
        public float hoverAmplitude = 0.15f;

        // 開始位置を保存する変数
        private Vector3 startLocalPosition;

        void Start()
        {
            // 初期ローカル位置を記録します
            startLocalPosition = transform.localPosition;
        }

        void Update()
        {
            // サイン波を使って滑らかに上下に往復させます
            float newY = startLocalPosition.y + Mathf.Sin(Time.time * hoverSpeed) * hoverAmplitude;
            transform.localPosition = new Vector3(startLocalPosition.x, newY, startLocalPosition.z);
        }
    }
}
