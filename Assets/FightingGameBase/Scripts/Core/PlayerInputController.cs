using UnityEngine;
using UnityEngine.InputSystem;

namespace FightingGameBase
{
    // =================================================================================
    // 【PlayerInputController（プレイヤーの入力・操作）】
    // このスクリプトをアタッチするだけで、キーボード入力でキャラクターを動かせるようになります。
    // [RequireComponent] と書くと、必ず「CharacterBase」も一緒に付きます。
    // =================================================================================
    [RequireComponent(typeof(CharacterBase))]
    public class PlayerInputController : MonoBehaviour
    {
        [Header("キー設定 (インスペクターで自由に変更可能)")]
        
        // Key.○○ と設定しておくことで、どのキーを押したら反応するかを変更できます
        public Key leftKey = Key.A;             // 左移動
        public Key rightKey = Key.D;            // 右移動
        public Key jumpKey = Key.Space;         // ジャンプ
        public Key normalAttackKey = Key.J;     // 通常攻撃
        public Key specialAttackKey = Key.K;    // 特殊攻撃
        public Key blockKey = Key.H;            // ブロック・パリー
        public Key dashKey = Key.L;             // ダッシュ・回避
        public Key stunAttackKey = Key.C;       // スタンスキル（行動不能）

        [Header("設定")]
        [Tooltip("通常攻撃キーと特殊攻撃キーの同時押しと判定する猶予時間（秒）。0.05秒くらいがちょうどいいです")]
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
            InitializeInputsByPosition();
        }

        private void InitializeInputsByPosition()
        {
            PlayerInputController[] controllers = FindObjectsByType<PlayerInputController>(FindObjectsSortMode.None);

            // Sort by X position to determine playerID and assign keys to all controllers in the scene
            if (controllers.Length > 1)
            {
                System.Array.Sort(controllers, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
                for (int i = 0; i < controllers.Length; i++)
                {
                    var ctrl = controllers[i];
                    var charBase = ctrl.GetComponent<CharacterBase>();
                    if (charBase != null)
                    {
                        charBase.playerID = i + 1;
                    }

                    if (i == 0) // Player 1
                    {
                        ctrl.leftKey = Key.A;
                        ctrl.rightKey = Key.D;
                        ctrl.jumpKey = Key.Space;
                        ctrl.normalAttackKey = Key.J;
                        ctrl.specialAttackKey = Key.K;
                        ctrl.blockKey = Key.H;
                        ctrl.dashKey = Key.L;
                        ctrl.stunAttackKey = Key.C;
                    }
                    else if (i == 1) // Player 2
                    {
                        ctrl.leftKey = Key.LeftArrow;
                        ctrl.rightKey = Key.RightArrow;
                        ctrl.jumpKey = Key.UpArrow;
                        ctrl.normalAttackKey = Key.Numpad1;
                        ctrl.specialAttackKey = Key.Numpad2;
                        ctrl.blockKey = Key.Numpad3;
                        ctrl.dashKey = Key.Numpad5;
                        ctrl.stunAttackKey = Key.Numpad0;
                    }
                }
            }
            else
            {
                // Fallback for single controller setup
                if (character != null)
                {
                    if (character.playerID == 1)
                    {
                        leftKey = Key.A;
                        rightKey = Key.D;
                        jumpKey = Key.Space;
                        normalAttackKey = Key.J;
                        specialAttackKey = Key.K;
                        blockKey = Key.H;
                        dashKey = Key.L;
                        stunAttackKey = Key.C;
                    }
                    else if (character.playerID == 2)
                    {
                        leftKey = Key.LeftArrow;
                        rightKey = Key.RightArrow;
                        jumpKey = Key.UpArrow;
                        normalAttackKey = Key.Numpad1;
                        specialAttackKey = Key.Numpad2;
                        blockKey = Key.Numpad3;
                        dashKey = Key.Numpad5;
                        stunAttackKey = Key.Numpad0;
                    }
                }
            }
        }

        private bool IsKeyPressed(Key key)
        {
            if (Keyboard.current == null) return false;

            // If checking a key for Player 2, allow non-Numpad fallbacks
            if (character != null && character.playerID == 2)
            {
                if (key == Key.Numpad1 && Keyboard.current[Key.U].isPressed) return true;
                if (key == Key.Numpad2 && Keyboard.current[Key.I].isPressed) return true;
                if (key == Key.Numpad3 && Keyboard.current[Key.O].isPressed) return true;
                if (key == Key.Numpad5 && Keyboard.current[Key.P].isPressed) return true;
                if (key == Key.Numpad0 && Keyboard.current[Key.Y].isPressed) return true;
            }

            return Keyboard.current[key].isPressed;
        }

        private bool WasKeyPressedThisFrame(Key key)
        {
            if (Keyboard.current == null) return false;

            if (character != null && character.playerID == 2)
            {
                if (key == Key.Numpad1 && Keyboard.current[Key.U].wasPressedThisFrame) return true;
                if (key == Key.Numpad2 && Keyboard.current[Key.I].wasPressedThisFrame) return true;
                if (key == Key.Numpad3 && Keyboard.current[Key.O].wasPressedThisFrame) return true;
                if (key == Key.Numpad5 && Keyboard.current[Key.P].wasPressedThisFrame) return true;
                if (key == Key.Numpad0 && Keyboard.current[Key.Y].wasPressedThisFrame) return true;
            }

            return Keyboard.current[key].wasPressedThisFrame;
        }

        private bool WasKeyReleasedThisFrame(Key key)
        {
            if (Keyboard.current == null) return false;

            if (character != null && character.playerID == 2)
            {
                if (key == Key.Numpad1 && Keyboard.current[Key.U].wasReleasedThisFrame) return true;
                if (key == Key.Numpad2 && Keyboard.current[Key.I].wasReleasedThisFrame) return true;
                if (key == Key.Numpad3 && Keyboard.current[Key.O].wasReleasedThisFrame) return true;
                if (key == Key.Numpad5 && Keyboard.current[Key.P].wasReleasedThisFrame) return true;
                if (key == Key.Numpad0 && Keyboard.current[Key.Y].wasReleasedThisFrame) return true;
            }

            return Keyboard.current[key].wasReleasedThisFrame;
        }

        void Update()
        {
            // キャラクターがいない、または倒れている場合、スタン中、被弾反動(IsHurtLocked)中は入力を受け付けません
            if (character == null || character.isDead || character.isStunned || character.IsHurtLocked) return;

            // キーボードが接続されていない場合も何もしません
            if (Keyboard.current == null) return;

            // 移動の処理と、攻撃の処理をそれぞれ呼び出します
            HandleMovement();
            if (character.IsAttacking) return;
            HandleAttacks();
            HandleBlock();
        }

        // ==========================================
        // 移動とジャンプの処理
        // ==========================================
        private void HandleMovement()
        {
            float direction = 0f;
            
            // 左キーが押されていれば -1 に、右キーが押されていれば +1 にします
            if (IsKeyPressed(leftKey)) direction -= 1f;
            if (IsKeyPressed(rightKey)) direction += 1f;

            // キャラクター本体に「この方向に移動して！」と命令します
            character.Move(direction);

            // 攻撁E中ならダッシュやジャンプは制限しまぁE
            if (character.IsAttacking) return;

            // ダッシュ・回避キー判定
            if (WasKeyPressedThisFrame(dashKey))
            {
                character.TriggerDashOrEvade(direction);
            }

            // ジャンプキーが「押された瞬間（wasPressedThisFrame）」ならジャンプします
            if (WasKeyPressedThisFrame(jumpKey))
            {
                character.Jump();
            }
        }

        // ==========================================
        // 攻撃と「同時押し」の処理
        // ==========================================
        private void HandleAttacks()
        {
            // キーが「押された瞬間」かどうかをチェックします
            bool normalAttackDown = WasKeyPressedThisFrame(normalAttackKey);
            bool specialAttackDown = WasKeyPressedThisFrame(specialAttackKey);

            // 押されたら、その時のゲーム内時間を記録しておきます
            if (normalAttackDown) lastNormalAttackPressTime = Time.time;
            if (specialAttackDown) lastSpecialAttackPressTime = Time.time;

            // --- 同時押しの判定 ---
            bool isSimultaneous = false;
            // 通常攻撃キーと特殊攻撃キーの両方が、最近（0.05秒以内）押されたかをチェックします
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
                
                // 通常攻撃 (J)
                // 押してから少し時間が経った（同時押しじゃないと確定した）場合に出ます
                if (lastNormalAttackPressTime > 0 && Time.time - lastNormalAttackPressTime > simultaneousPressWindow)
                {
                    character.AttackNormal();
                    lastNormalAttackPressTime = -1f;
                    isUltimateTriggered = false;
                }
                
                // 特殊攻撃 (K)
                if (lastSpecialAttackPressTime > 0 && Time.time - lastSpecialAttackPressTime > simultaneousPressWindow)
                {
                    character.AttackSpecial();
                    lastSpecialAttackPressTime = -1f;
                    isUltimateTriggered = false;
                }
            }
            
            // キーを「離した瞬間」にロックを解除して、また必殺技を出せるようにします
            if (WasKeyReleasedThisFrame(normalAttackKey) || WasKeyReleasedThisFrame(specialAttackKey))
            {
                isUltimateTriggered = false;
            }

            // --- スタンスキル (C) ---
            if (WasKeyPressedThisFrame(stunAttackKey))
            {
                character.AttackStun();
            }
        }

        // ==========================================
        // ブロック・パリーの処理
        // ==========================================
        private void HandleBlock()
        {
            // Hキーを押した瞬間 → StartBlock（パリー窓を開く）
            if (WasKeyPressedThisFrame(blockKey))
            {
                character.StartBlock();
            }
            // Hキーを離した瞬間 → StopBlock（ブロック解除）
            if (WasKeyReleasedThisFrame(blockKey))
            {
                character.StopBlock();
            }
        }
    }
}
