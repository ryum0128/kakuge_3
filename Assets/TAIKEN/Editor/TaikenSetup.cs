#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FightingGameBase.Editor
{
    public class TaikenSetup
    {
        [MenuItem("FightingGame/大剣キャラクターを作成する")]
        public static void CreateTaiken()
        {
            // 1. Create Root Object
            GameObject root = new GameObject("Taiken");
            
            Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3.0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CapsuleCollider2D hurtCollider = root.AddComponent<CapsuleCollider2D>();
            hurtCollider.size = new Vector2(1.0f, 2.0f);
            hurtCollider.offset = new Vector2(0f, 0f); 

            TaikenCharacter characterBase = root.AddComponent<TaikenCharacter>();
            Hurtbox hurtbox = root.AddComponent<Hurtbox>();
            hurtbox.owner = characterBase;
            
            PlayerInputController inputController = root.AddComponent<PlayerInputController>();
            inputController.leftKey = UnityEngine.InputSystem.Key.A;
            inputController.rightKey = UnityEngine.InputSystem.Key.D;
            inputController.jumpKey = UnityEngine.InputSystem.Key.Space;
            inputController.normalAttackKey = UnityEngine.InputSystem.Key.J;
            inputController.specialAttackKey = UnityEngine.InputSystem.Key.K;

            // 2. Create Visuals Hierarchy (Simple rectangle character)
            GameObject visuals = new GameObject("Visuals");
            visuals.transform.SetParent(root.transform);
            visuals.transform.localScale = Vector3.one;
            visuals.transform.localPosition = Vector3.zero;
            
            SpriteRenderer sr = visuals.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(1f, 2f);
            
            // 3. Create Weapon visual (Simple rectangle sword matching hitbox size ratio)
            GameObject weapon = new GameObject("Weapon");
            weapon.transform.SetParent(visuals.transform);
            weapon.transform.localPosition = new Vector3(0.7f, 0.6f, 0f);
            weapon.transform.localScale = Vector3.one; // Keep localScale at 1.0!

            SpriteRenderer weaponSr = weapon.AddComponent<SpriteRenderer>();
            weaponSr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            weaponSr.drawMode = SpriteDrawMode.Sliced; // Use Sliced for precise sizing!
            weaponSr.size = new Vector2(1.2f, 0.8f); // Exact match with collider bounds!
            weaponSr.color = new Color(0.6f, 0.6f, 0.6f, 1f); // Grey color
            weaponSr.sortingOrder = 1;

            visuals.AddComponent<Animator>();
            visuals.AddComponent<HoverEffect>();

            // 4. Detached Hitbox Hierarchy (PunchHitbox)
            GameObject hitboxObj = new GameObject("PunchHitbox");
            hitboxObj.transform.SetParent(root.transform);
            hitboxObj.transform.localPosition = new Vector3(0.7f, 0.6f, 0f); 
            
            BoxCollider2D hitCollider = hitboxObj.AddComponent<BoxCollider2D>();
            hitCollider.isTrigger = true;
            hitCollider.size = new Vector2(1.2f, 0.8f);

            Hitbox hitbox = hitboxObj.AddComponent<Hitbox>();
            hitbox.damage = 15;
            hitbox.ownerPlayerID = 1;

            hitboxObj.SetActive(false);

            // 5. BlockShield Hierarchy (under Visuals to match search path)
            GameObject shieldObj = new GameObject("BlockShield");
            shieldObj.transform.SetParent(visuals.transform);
            shieldObj.transform.localPosition = new Vector3(0.2f, 0.2f, 0f);

            SpriteRenderer shieldSr = shieldObj.AddComponent<SpriteRenderer>();
            shieldSr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            shieldSr.color = new Color(0.3f, 0.8f, 1.0f, 0.55f);
            shieldSr.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
            shieldSr.sortingOrder = 1;

            shieldObj.SetActive(false);

            // 6. CharacterStats asset settings
            string folderPath = "Assets/TAIKEN";
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }

            string statsPath = folderPath + "/TaikenStats.asset";
            CharacterStats stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(statsPath);
            if (stats == null)
            {
                stats = ScriptableObject.CreateInstance<CharacterStats>();
                stats.maxHP = 120;
                stats.moveSpeed = 6f;
                stats.jumpForce = 13f;
                AssetDatabase.CreateAsset(stats, statsPath);
                AssetDatabase.SaveAssets();
            }
            characterBase.stats = stats;

            // 7. Save prefab as Assets/TAIKEN/Taiken.prefab
            string prefabPath = folderPath + "/Taiken.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction);

            Selection.activeGameObject = root;
            
            Debug.Log($"【大剣作成完了】Taikenプレハブを生成しました！\n場所: {prefabPath}");
        }

        [MenuItem("FightingGame/選択したオブジェクトをサンドバックAIにする")]
        public static void MakeSelectedSandbag()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogError("オブジェクトを選択してください。");
                return;
            }

            CharacterBase character = selected.GetComponent<CharacterBase>();
            if (character == null)
            {
                Debug.LogError("CharacterBaseがアタッチされたオブジェクトを選択してください。");
                return;
            }

            // Remove PlayerInputController to prevent player control input conflicts
            PlayerInputController input = selected.GetComponent<PlayerInputController>();
            if (input != null)
            {
                Object.DestroyImmediate(input);
            }

            // Attach or get SandbagAI script
            SandbagAI ai = selected.GetComponent<SandbagAI>();
            if (ai == null)
            {
                ai = selected.AddComponent<SandbagAI>();
            }

            // Ensure the character has a visual Weapon matching its PunchHitbox size
            Transform visuals = selected.transform.Find("Visuals");
            if (visuals != null)
            {
                Transform weapon = visuals.Find("Weapon");
                if (weapon == null)
                {
                    // Create Weapon
                    GameObject weaponObj = new GameObject("Weapon");
                    weaponObj.transform.SetParent(visuals);
                    weapon = weaponObj.transform;

                    // Match PunchHitbox position and size
                    Transform hitbox = selected.transform.Find("PunchHitbox");
                    Vector3 weaponPos = new Vector3(0.8f, 0.5f, 0f);
                    Vector2 weaponSize = new Vector2(0.8f, 0.5f);

                    if (hitbox != null)
                    {
                        weaponPos = hitbox.localPosition;
                        BoxCollider2D col = hitbox.GetComponent<BoxCollider2D>();
                        if (col != null)
                        {
                            weaponSize = col.size;
                        }
                    }

                    weapon.localPosition = weaponPos;
                    weapon.localScale = Vector3.one;

                    SpriteRenderer weaponSr = weaponObj.AddComponent<SpriteRenderer>();
                    weaponSr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                    weaponSr.drawMode = SpriteDrawMode.Sliced;
                    weaponSr.size = weaponSize;
                    weaponSr.color = new Color(0.6f, 0.6f, 0.6f, 1f); // Grey
                    weaponSr.sortingOrder = 1;

                    Debug.Log($"[TaikenSetup] {selected.name}に武器(Weapon)を自動追加しました。(サイズ: {weaponSize}, 位置: {weaponPos})");
                }
            }

            Debug.Log($"【サンドバック化完了】{selected.name} を不死身 ＆ 自動通常攻撃を行うAIに設定しました！");
        }

        [MenuItem("FightingGame/選択したオブジェクトに対戦CPU(AI)をアタッチする")]
        public static void MakeSelectedVersusCPU()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogError("オブジェクトを選択してください。");
                return;
            }

            CharacterBase character = selected.GetComponent<CharacterBase>();
            if (character == null)
            {
                Debug.LogError("CharacterBaseがアタッチされたオブジェクトを選択してください。");
                return;
            }

            // Remove PlayerInputController to prevent player control input conflicts
            PlayerInputController input = selected.GetComponent<PlayerInputController>();
            if (input != null)
            {
                Object.DestroyImmediate(input);
            }

            // Remove SandbagAI if attached
            SandbagAI sandbag = selected.GetComponent<SandbagAI>();
            if (sandbag != null)
            {
                Object.DestroyImmediate(sandbag);
            }

            // Attach VersusCPUAI
            VersusCPUAI cpu = selected.GetComponent<VersusCPUAI>();
            if (cpu == null)
            {
                cpu = selected.AddComponent<VersusCPUAI>();
            }

            character.playerID = 2;

            Debug.Log($"【対戦CPU化完了】{selected.name} に対戦型CPU(AI)をセットアップしました！");
        }
    }
}
#endif
