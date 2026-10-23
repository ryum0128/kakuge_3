using UnityEngine;
using UnityEngine.InputSystem;

namespace FightingGameBase
{
    // =================================================================================
    // 【GamepadInputController（ゲームパッドの入力・操作）】
    // Xboxコントローラーなどのゲームパッドでキャラクターを操作するためのスクリプトです。
    // 1Pは1番目のゲームパッド、2Pは2番目のゲームパッドで操作します。
    // [RequireComponent] と書くと、必ず「CharacterBase」も一緒に付きます。
    //
    // 【操作方法】
    //   左スティック / 十字キー … 移動
    //   Aボタン（South）       … ジャンプ
    //   Xボタン（West）        … 通常攻撃
    //   Yボタン（North）       … 特殊攻撃
    //   X + Y 同時押し         … 必殺技
    //   RBボタン（RightShoulder）… ブロック・パリー
    // =================================================================================
    public enum GamepadButton
    {
        South,
        North,
        East,
        West,
        LeftShoulder,
        RightShoulder,
        LeftStickButton,
        RightStickButton,
        Start,
        Select,
        LeftTrigger,
        RightTrigger
    }

    [RequireComponent(typeof(CharacterBase))]
    public class GamepadInputController : MonoBehaviour
    {
        [Header("ゲームパッド設定")]
        [Tooltip("何番目のゲームパッドを使うか（0 = 1番目, 1 = 2番目）。1Pなら0、2Pなら1にしてください")]
        public int gamepadIndex = 0;

        [Header("ボタン設定（インスペクターで自由に変更可能）")]
        [Tooltip("ジャンプに使うボタン")]
        public GamepadButton jumpButton = GamepadButton.South;          // Aボタン
        [Tooltip("通常攻撃に使うボタン")]
        public GamepadButton normalAttackButton = GamepadButton.West;   // Xボタン
        [Tooltip("特殊攻撃に使うボタン")]
        public GamepadButton specialAttackButton = GamepadButton.North; // Yボタン
        [Tooltip("ブロック・パリーに使うボタン")]
        public GamepadButton blockButton = GamepadButton.RightShoulder; // RBボタン

        [Header("設定")]
        [Tooltip("左スティックのデッドゾーン（この値以下の傾きは無視されます）")]
        public float stickDeadZone = 0.2f;

        [Tooltip("通常攻撃と特殊攻撃の同時押しと判定する猶予時間（秒）。0.05秒くらいがちょうどいいです")]
        public float simultaneousPressWindow = 0.05f;

        private CharacterBase character; // キャラクター本体を操作するためのリモコンのようなもの

        // --- 入力タイミングの記録用（同時押し判定のために使います） ---
        private float lastNormalAttackPressTime = -1f;
        private float lastSpecialAttackPressTime = -1f;
        private bool isUltimateTriggered = false; // 必殺技が出たかどうか

        void Start()
        {
            // キャラクター本体（CharacterBase）を見つけて取得します
            character = GetComponent<CharacterBase>();
        }

        /// <summary>
        /// 接続されているゲームパッドの中から、自分の番号に対応するものを取得します。
        /// 接続されていなければ null を返します。
        /// </summary>
        private Gamepad GetMyGamepad()
        {
            // 現在接続されている全ゲームパッドのリストから、自分のインデックスのものを取得
            var gamepads = Gamepad.all;
            if (gamepadIndex < gamepads.Count)
            {
                return gamepads[gamepadIndex];
            }
            return null;
        }

        /// <summary>
        /// GamepadButton の列挙値から、実際のボタンコントロールを取得するヘルパーです。
        /// </summary>
        private UnityEngine.InputSystem.Controls.ButtonControl GetButton(Gamepad pad, GamepadButton button)
        {
            switch (button)
            {
                case GamepadButton.South:         return pad.buttonSouth;
                case GamepadButton.North:         return pad.buttonNorth;
                case GamepadButton.East:          return pad.buttonEast;
                case GamepadButton.West:          return pad.buttonWest;
                case GamepadButton.LeftShoulder:   return pad.leftShoulder;
                case GamepadButton.RightShoulder:  return pad.rightShoulder;
                case GamepadButton.LeftStickButton: return pad.leftStickButton;
                case GamepadButton.RightStickButton: return pad.rightStickButton;
                case GamepadButton.Start:         return pad.startButton;
                case GamepadButton.Select:        return pad.selectButton;
                // LeftTrigger / RightTrigger はボタンとしても使える
                case GamepadButton.LeftTrigger:   return pad.leftTrigger;
                case GamepadButton.RightTrigger:  return pad.rightTrigger;
                default:                          return pad.buttonSouth;
            }
        }

        /// <summary>
        /// 特殊攻撃ボタンが長押しされているかどうかを返します。
        /// </summary>
        public bool IsSpecialAttackHeld()
        {
            Gamepad pad = GetMyGamepad();
            if (pad != null)
            {
                return GetButton(pad, specialAttackButton).isPressed;
            }
            return false;
        }

        void Update()
        {
            // キャラクターがいない、または倒れている場合は入力を受け付けません
            if (character == null || character.isDead) return;

            // 自分の番号に対応するゲームパッドを取得します
            Gamepad pad = GetMyGamepad();
            if (pad == null) return; // ゲームパッドが接続されていなければ何もしません

            // 移動・攻撃・ブロックの処理をそれぞれ呼び出します
            HandleMovement(pad);
            HandleAttacks(pad);
            HandleBlock(pad);
        }

        // ==========================================
        // 移動とジャンプの処理
        // ==========================================
        private void HandleMovement(Gamepad pad)
        {
            // 左スティックの横方向の値を取得（-1.0 〜 +1.0）
            float direction = pad.leftStick.x.ReadValue();

            // 十字キー（D-Pad）の入力も受け付けます（どちらかが入力されていれば優先）
            if (pad.dpad.left.isPressed) direction = -1f;
            if (pad.dpad.right.isPressed) direction = 1f;

            // デッドゾーン処理（スティックの微細なブレを無視します）
            if (Mathf.Abs(direction) < stickDeadZone) direction = 0f;

            // キャラクター本体に「この方向に移動して！」と命令します
            character.Move(direction);

            // ジャンプボタンが「押された瞬間」ならジャンプします
            if (GetButton(pad, jumpButton).wasPressedThisFrame)
            {
                character.Jump();
            }
        }

        // ==========================================
        // 攻撃と「同時押し」の処理
        // ==========================================
        private void HandleAttacks(Gamepad pad)
        {
            // ボタンが「押された瞬間」かどうかをチェックします
            bool normalAttackDown = GetButton(pad, normalAttackButton).wasPressedThisFrame;
            bool specialAttackDown = GetButton(pad, specialAttackButton).wasPressedThisFrame;

            // 押されたら、その時のゲーム内時間を記録しておきます
            if (normalAttackDown) lastNormalAttackPressTime = Time.time;
            if (specialAttackDown) lastSpecialAttackPressTime = Time.time;

            // --- 同時押しの判定 ---
            bool isSimultaneous = false;
            // 通常攻撃ボタンと特殊攻撃ボタンの両方が、最近（0.05秒以内）押されたかをチェックします
            if (Time.time - lastNormalAttackPressTime <= simultaneousPressWindow &&
                Time.time - lastSpecialAttackPressTime <= simultaneousPressWindow)
            {
                isSimultaneous = true; // 同時押し成功！
            }

            if (isSimultaneous)
            {
                // 同時押し（必殺技）
                if (!isUltimateTriggered)
                {
                    character.AttackUltimate();
                    isUltimateTriggered = true; // 暴発を防ぐためのロックをかけます

                    // 次の攻撃を出せるように判定をリセットします
                    lastNormalAttackPressTime = -1f;
                    lastSpecialAttackPressTime = -1f;
                }
            }
            else
            {
                // --- 単発入力の処理（同時押しの猶予時間を過ぎていたら発動します） ---

                // 通常攻撃 (Xボタン)
                // 押してから少し時間が経った（同時押しじゃないと確定した）場合に出ます
                if (lastNormalAttackPressTime > 0 && Time.time - lastNormalAttackPressTime > simultaneousPressWindow)
                {
                    character.AttackNormal();
                    lastNormalAttackPressTime = -1f;
                    isUltimateTriggered = false;
                }

                // 特殊攻撃 (Yボタン)
                if (lastSpecialAttackPressTime > 0 && Time.time - lastSpecialAttackPressTime > simultaneousPressWindow)
                {
                    character.AttackSpecial();
                    lastSpecialAttackPressTime = -1f;
                    isUltimateTriggered = false;
                }
            }

            // ボタンを「離した瞬間」にロックを解除して、また必殺技を出せるようにします
            if (GetButton(pad, normalAttackButton).wasReleasedThisFrame || GetButton(pad, specialAttackButton).wasReleasedThisFrame)
            {
                isUltimateTriggered = false;
            }
        }

        // ==========================================
        // ブロック・パリーの処理
        // ==========================================
        private void HandleBlock(Gamepad pad)
        {
            // ブロックボタンを押した瞬間 → StartBlock（パリー窓を開く）
            if (GetButton(pad, blockButton).wasPressedThisFrame)
            {
                character.StartBlock();
            }
            // ブロックボタンを離した瞬間 → StopBlock（ブロック解除）
            if (GetButton(pad, blockButton).wasReleasedThisFrame)
            {
                character.StopBlock();
            }
        }
    }
}
