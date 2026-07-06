using UnityEngine;
using System.Collections;

namespace FightingGameBase
{
    // =================================================================================
    // 【NPCPuncher（殴ってくる練習用NPC）】
    // このスクリプトをアタッチしたキャラクターは、一定時間おきに通常攻撃（パンチ）を繰り返します。
    // パリーやブロックのテスト用として使用します。
    // =================================================================================
    public class NPCPuncher : MonoBehaviour
    {
        private CharacterBase character;
        private Rigidbody2D rb;
        private Hitbox hitbox;
        private GameObject hitboxObj;

        [Tooltip("攻撃の間隔（秒）")]
        public float attackInterval = 2.5f;

        [Tooltip("最初の攻撃までの遅延時間（秒）")]
        public float startDelay = 1.0f;

        [Tooltip("攻撃判定を出す時間（秒）- 長いほどパリーしやすい")]
        public float hitboxDuration = 0.5f;

        [Tooltip("プレイヤーを追いかける速度")]
        public float chaseSpeed = 1.5f;

        [Tooltip("攻撃を出す距離（この距離以内になったら攻撃）")]
        public float attackRange = 2.5f;

        private Transform playerTarget;
        private bool isAttacking = false;

        void Start()
        {
            character = GetComponent<CharacterBase>();
            rb = GetComponent<Rigidbody2D>();

            if (character == null)
            {
                Debug.LogError("<b>NPCPuncher エラー</b>: 同一オブジェクトに CharacterBase が見つかりません！");
                enabled = false;
                return;
            }

            // プレイヤーからの入力を無効化するために PlayerInputController をオフにする
            PlayerInputController input = GetComponent<PlayerInputController>();
            if (input != null) input.enabled = false;

            // プレイヤーIDを2Pに設定
            character.playerID = 2;

            // ヒットボックスを取得してIDを2Pに設定
            hitbox = GetComponentInChildren<Hitbox>(true);
            if (hitbox != null)
            {
                hitbox.ownerPlayerID = 2;
                hitboxObj = hitbox.gameObject;
                hitboxObj.SetActive(false);
            }

            // 敵キャラクターを左向きにする
            transform.localScale = new Vector3(-1f, 1f, 1f);

            // playerIDが1のキャラクター（プレイヤー）を探す
            CharacterBase[] allChars = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
            foreach (var c in allChars)
            {
                if (c.playerID == 1)
                {
                    playerTarget = c.transform;
                    break;
                }
            }

            // ループ攻撃コルーチンを開始
            StartCoroutine(BehaviorRoutine());
        }

        void FixedUpdate()
        {
            if (character.isDead || playerTarget == null || isAttacking) return;

            // プレイヤーとの距離を計算
            float dist = playerTarget.position.x - transform.position.x;
            float absDist = Mathf.Abs(dist);

            // 攻撃範囲外なら近づく
            if (absDist > attackRange)
            {
                float dir = Mathf.Sign(dist);
                rb.linearVelocity = new Vector2(-dir * chaseSpeed, rb.linearVelocity.y); // 左向きなので逆方向
                // 向きをプレイヤー方向に変更（常に左向き=プレイヤーの右にいる想定）
                transform.localScale = new Vector3(dist > 0 ? -1f : 1f, 1f, 1f);
            }
            else
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
        }

        private IEnumerator BehaviorRoutine()
        {
            yield return new WaitForSeconds(startDelay);

            while (!character.isDead)
            {
                // 攻撃範囲内なら攻撃する
                if (playerTarget != null && Mathf.Abs(playerTarget.position.x - transform.position.x) <= attackRange)
                {
                    yield return StartCoroutine(DoAttack());
                }

                yield return new WaitForSeconds(attackInterval);
            }
        }

        private IEnumerator DoAttack()
        {
            isAttacking = true;

            // ★ 攻撃前の短い溜め（プレイヤーがHキーを押す余裕を作る）
            // 停止して少し待つ
            if (rb != null) rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            yield return new WaitForSeconds(0.4f);

            // ヒットボックスを有効化（長めに出すことでパリー・ブロックの練習ができる）
            if (hitboxObj != null)
            {
                hitboxObj.SetActive(true);
                Debug.Log("NPC: 攻撃！（パリー受付窓中にHキーを押してください）");
                yield return new WaitForSeconds(hitboxDuration);
                hitboxObj.SetActive(false);
            }

            isAttacking = false;
        }
    }
}
