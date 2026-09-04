using UnityEngine;
using UnityEngine.InputSystem;

namespace FightingGameBase
{
    // =================================================================================
    // 【HammerInputController（ハンマー専用の入力コントローラー）】
    // お手本の PlayerInputController をベースに、ハンマーキャラクター用に作りました。
    //
    // 【設計のポイント】
    //   GannrannsuInputController と同様に、HammerCharacter 型のメソッドを直接呼び出すことで
    //   ハンマー固有の「AttackNormal()」「AttackSpecial()」「AttackUltimate()」が正しく実行されます。
    //   ZとXの同時押し時に、必殺技がクールタイム中だった場合は通常攻撃（突き）へフォールバックします。
    // =================================================================================
    [RequireComponent(typeof(HammerCharacter))]
    public class HammerInputController : MonoBehaviour
    {
        [Header("キー設定 (インスペクターで自由に変更可能)")]
        public Key leftKey = Key.A;              // 左移動
        public Key rightKey = Key.D;             // 右移動
        public Key jumpKey = Key.W;              // ジャンプ
        public Key normalAttackKey = Key.K;      // 通常攻撃（ハンマー横振り）
        public Key specialAttackKey = Key.L;     // 特殊攻撃（ハンマー叩きつけ）

        [Header("設定")]
        [Tooltip("ZとXの同時押しと判定する猶予時間（秒）。0.05秒くらいがちょうどいいです")]
        public float simultaneousPressWindow = 0.05f;

        private HammerCharacter character;

        // --- 入力タイミングの記録用 ---
        private float lastZPressTime = -1f;
        private float lastXPressTime = -1f;
        private bool isUltimateTriggered = false;

        private bool keysInitialized = false;

        void Awake()
        {
            character = GetComponent<HammerCharacter>();
        }

        void Start()
        {
            if (character == null)
            {
                character = GetComponent<HammerCharacter>();
            }
        }

        private void InitializeKeys()
        {
            if (character != null && (character.playerID == 2 || name.Contains("P2") || name.Contains("RightSide") || name.Contains("Player2")))
            {
                leftKey = Key.LeftArrow;
                rightKey = Key.RightArrow;
                jumpKey = Key.UpArrow;
                normalAttackKey = Key.I;
                specialAttackKey = Key.O;
            }
            keysInitialized = true;
        }

        void Update()
        {
            if (!keysInitialized)
            {
                InitializeKeys();
            }

            if (character == null || character.isDead) return;
            if (Keyboard.current == null) return;

            HandleMovement();
            HandleAttacks();
        }

        private void HandleMovement()
        {
            float direction = 0f;

            if (Keyboard.current[leftKey].isPressed) direction -= 1f;
            if (Keyboard.current[rightKey].isPressed) direction += 1f;

            // キャラクターに移動方向を指示します
            character.Move(direction);

            if (Keyboard.current[jumpKey].wasPressedThisFrame)
            {
                character.Jump();
            }
        }

        private void HandleAttacks()
        {
            // キーが「押された瞬間」かどうかをチェックします
            bool zDown = Keyboard.current[normalAttackKey].wasPressedThisFrame;
            bool xDown = Keyboard.current[specialAttackKey].wasPressedThisFrame;

            // 押されたら、その時のゲーム内時間を記録しておきます
            if (zDown) lastZPressTime = Time.time;
            if (xDown) lastXPressTime = Time.time;

            // --- 同時押しの判定 ---
            bool isSimultaneous = false;
            // ZキーとXキーの両方が、最近（0.05秒以内）押されたかをチェックします
            if (lastZPressTime > 0 && lastXPressTime > 0 &&
                Time.time - lastZPressTime <= simultaneousPressWindow &&
                Time.time - lastXPressTime <= simultaneousPressWindow)
            {
                isSimultaneous = true; // 同時押し成功！
            }

            if (isSimultaneous)
            {
                // 同時押し（必殺技）
                if (!isUltimateTriggered)
                {
                    if (character.chargeGauge >= character.maxChargeGauge && character.ultimateAttackCooldownTimer <= 0f)
                    {
                        character.AttackUltimate(); // ゲージ満タンかつクールタイム中ではないなら必殺技（ギガスマッシュ）発動！
                        isUltimateTriggered = true; // ボタン押しっぱなしによる連続発射を防ぐ

                        // 同時押しが成功したため、単発攻撃（通常・特殊）の予約をクリアします
                        lastZPressTime = -1f;
                        lastXPressTime = -1f;
                    }
                    else
                    {
                        Debug.Log("ギガスマッシュ：チャージ不足またはクールタイム中！代わりに通常攻撃を発動します。");
                        character.AttackNormal(); // 発動できない場合は通常攻撃を発動
                        isUltimateTriggered = true; // ボタン押しっぱなしによる連続発射を防ぐ

                        // 通常攻撃を発動したため、単発攻撃の予約をクリアします
                        lastZPressTime = -1f;
                        lastXPressTime = -1f;
                    }
                }
            }
            else
            {
                // --- 単発入力の処理（同時押し猶予時間が経過した場合） ---
                
                // 通常攻撃 (Z)
                if (lastZPressTime > 0 && Time.time - lastZPressTime > simultaneousPressWindow)
                {
                    character.AttackNormal();
                    lastZPressTime = -1f;
                }

                // 特殊攻撃 (X)
                if (lastXPressTime > 0 && Time.time - lastXPressTime > simultaneousPressWindow)
                {
                    character.AttackSpecial();
                    lastXPressTime = -1f;
                }
            }

            // ボタンが離されたら同時押しロックを解除
            if (Keyboard.current[normalAttackKey].wasReleasedThisFrame || Keyboard.current[specialAttackKey].wasReleasedThisFrame)
            {
                isUltimateTriggered = false;
            }
        }
    }
}
