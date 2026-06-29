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
        // [Header] をつけると、Inspectorで区切り線とタイトルが表示されます
        [Header("キー設定 (インスペクターで自由に変更可能)")]
        
        // Key.○○ と設定しておくことで、どのキーを押したら反応するかを変更できます
        public Key leftKey = Key.LeftArrow;      // 左移動
        public Key rightKey = Key.RightArrow;    // 右移動
        public Key jumpKey = Key.UpArrow;        // ジャンプ
        public Key normalAttackKey = Key.Z;      // 通常攻撃
        public Key specialAttackKey = Key.X;     // 特殊攻撃

        [Header("設定")]
        [Tooltip("ZとXの同時押しと判定する猶予時間（秒）。0.05秒くらいがちょうどいいです")]
        public float simultaneousPressWindow = 0.05f;

        private CharacterBase character; // キャラクター本体を操作するためのリモコンのようなもの
        
        // --- 入力タイミングの記録用（同時押し判定のために使います） ---
        private float lastZPressTime = -1f;
        private float lastXPressTime = -1f;
        private bool isUltimateTriggered = false; // 必殺技が出たかどうか

        void Start()
        {
            // キャラクター本体（CharacterBase）を見つけて取得します
            character = GetComponent<CharacterBase>();
        }

        void Update()
        {
            // キャラクターがいない、または倒れている場合は入力を受け付けません
            if (character == null || character.isDead) return;
            
            // キーボードが接続されていない場合も何もしません
            if (Keyboard.current == null) return;

            // 移動の処理と、攻撃の処理をそれぞれ呼び出します
            HandleMovement();
            HandleAttacks();
        }

        // ==========================================
        // 移動とジャンプの処理
        // ==========================================
        private void HandleMovement()
        {
            float direction = 0f;
            
            // 左キーが押されていれば -1 に、右キーが押されていれば +1 にします
            if (Keyboard.current[leftKey].isPressed) direction -= 1f;
            if (Keyboard.current[rightKey].isPressed) direction += 1f;

            // キャラクター本体に「この方向に移動して！」と命令します
            character.Move(direction);

            // ジャンプキーが「押された瞬間（wasPressedThisFrame）」ならジャンプします
            if (Keyboard.current[jumpKey].wasPressedThisFrame)
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
            bool zDown = Keyboard.current[normalAttackKey].wasPressedThisFrame;
            bool xDown = Keyboard.current[specialAttackKey].wasPressedThisFrame;

            // 押されたら、その時のゲーム内時間を記録しておきます
            if (zDown) lastZPressTime = Time.time;
            if (xDown) lastXPressTime = Time.time;

            // --- 同時押しの判定 ---
            bool isSimultaneous = false;
            // ZキーとXキーの両方が、最近（0.05秒以内）押されたかをチェックします
            if (Time.time - lastZPressTime <= simultaneousPressWindow && 
                Time.time - lastXPressTime <= simultaneousPressWindow)
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
                    lastZPressTime = -1f;
                    lastXPressTime = -1f;
                }
            }
            else
            {
                // --- 単発入力の処理（同時押しの猶予時間を過ぎていたら発動します） ---
                
                // 通常攻撃 (Z)
                // 押してから少し時間が経った（同時押しじゃないと確定した）場合に出ます
                if (lastZPressTime > 0 && Time.time - lastZPressTime > simultaneousPressWindow)
                {
                    character.AttackNormal();
                    lastZPressTime = -1f;
                    isUltimateTriggered = false;
                }
                
                // 特殊攻撃 (X)
                if (lastXPressTime > 0 && Time.time - lastXPressTime > simultaneousPressWindow)
                {
                    character.AttackSpecial();
                    lastXPressTime = -1f;
                    isUltimateTriggered = false;
                }
            }
            
            // キーを「離した瞬間」にロックを解除して、また必殺技を出せるようにします
            if (Keyboard.current[normalAttackKey].wasReleasedThisFrame || Keyboard.current[specialAttackKey].wasReleasedThisFrame)
            {
                isUltimateTriggered = false;
            }
        }
    }
}
