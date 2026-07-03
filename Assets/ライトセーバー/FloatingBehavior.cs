using UnityEngine;

namespace FightingGameBase
{
    // =================================================================================
    // 【FloatingBehavior（浮遊演出スクリプト）】
    // このスクリプトは、オブジェクトを上下にふわふわと浮遊させるためのものです。
    // 物理コライダーの位置（判定）はそのままに、見た目だけを浮かせることで
    // 格闘ゲームとしての操作感や当たり判定の挙動を壊さずに浮遊キャラクターを表現できます！
    // =================================================================================
    public class FloatingBehavior : MonoBehaviour
    {
        [Header("浮遊の設定")]
        [Tooltip("上下に揺れる幅（大きさ）")]
        public float amplitude = 0.15f; 
        
        [Tooltip("浮遊する速度（往復の速さ）")]
        public float frequency = 3f;    

        [Tooltip("基準となる浮き高さ")]
        public float baseOffset = 0.6f; 

        private Vector3 startPos;

        void Start()
        {
            // 親に対する相対的な初期位置を記録します
            startPos = transform.localPosition;
            // 基準のオフセット分だけ位置を上に上げます
            startPos.y += baseOffset;
        }

        void Update()
        {
            // 時間の経過とサイン波（Sin）を用いて、滑らかな上下往復運動を作ります
            float newY = startPos.y + Mathf.Sin(Time.time * frequency) * amplitude;
            transform.localPosition = new Vector3(startPos.x, newY, startPos.z);
        }
    }
}
