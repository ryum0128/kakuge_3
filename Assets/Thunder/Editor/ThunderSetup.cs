#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FightingGameBase.Editor
{
    public class ThunderSetup
    {
        [MenuItem("FightingGame/Thunderキャラクターを作成・再生成する")]
        public static void CreateThunderCharacter()
        {
            string folderPath = "Assets/Thunder";
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }

            // 1. ステータス（CharacterStats）の作成または取得
            string statsPath = folderPath + "/ThunderStats.asset";
            CharacterStats stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(statsPath);
            if (stats == null)
            {
                stats = ScriptableObject.CreateInstance<CharacterStats>();
                stats.maxHP = 110;
                stats.moveSpeed = 5.8f;
                stats.jumpForce = 12.5f;
                AssetDatabase.CreateAsset(stats, statsPath);
                AssetDatabase.SaveAssets();
            }

            // 2. ルートオブジェクトの構築
            GameObject root = new GameObject("ThunderCharacter");

            // 物理エンジン設定
            Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3.0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // やられ判定用コライダー（地面衝突・食らい判定）
            CapsuleCollider2D col = root.AddComponent<CapsuleCollider2D>();
            col.size = new Vector2(1.0f, 2.0f);
            col.offset = Vector2.zero;

            // ThunderCharacterコンポーネント
            ThunderCharacter character = root.AddComponent<ThunderCharacter>();
            character.stats = stats;

            // Hurtbox
            Hurtbox hurtbox = root.AddComponent<Hurtbox>();
            hurtbox.owner = character;

            // PlayerInputController
            PlayerInputController pic = root.AddComponent<PlayerInputController>();
            pic.leftKey = UnityEngine.InputSystem.Key.A;
            pic.rightKey = UnityEngine.InputSystem.Key.D;
            pic.jumpKey = UnityEngine.InputSystem.Key.W;
            pic.normalAttackKey = UnityEngine.InputSystem.Key.J;
            pic.specialAttackKey = UnityEngine.InputSystem.Key.K;
            pic.blockKey = UnityEngine.InputSystem.Key.L;

            // 3. Visuals（体は他と同じグレーの箱）
            GameObject visuals = new GameObject("Visuals");
            visuals.transform.SetParent(root.transform, false);

            SpriteRenderer bodySr = visuals.AddComponent<SpriteRenderer>();
            bodySr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            bodySr.drawMode = SpriteDrawMode.Sliced;
            bodySr.size = new Vector2(1.0f, 2.0f);
            bodySr.color = new Color(0.80f, 0.82f, 0.85f, 1.0f); // スタイリッシュなグレー色
            bodySr.sortingOrder = 0;

            visuals.AddComponent<Animator>();

            // 4. Hitbox（通常攻撃判定オブジェクト）
            GameObject hitboxObj = new GameObject("PunchHitbox");
            hitboxObj.transform.SetParent(root.transform, false);
            hitboxObj.transform.localPosition = new Vector3(1.6f, -0.4f, 0f);

            BoxCollider2D hitCol = hitboxObj.AddComponent<BoxCollider2D>();
            hitCol.isTrigger = true;
            hitCol.size = new Vector2(2.4f, 1.2f);

            Hitbox hitbox = hitboxObj.AddComponent<Hitbox>();
            hitbox.damage = 16;
            hitbox.ownerPlayerID = 1;
            hitbox.owner = character;
            hitboxObj.SetActive(false);

            // 5. プレハブの保存
            string prefabPath = folderPath + "/ThunderCharacter.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"【作成完了】Thunderキャラクターのプレハブを作成しました！\n場所: {prefabPath}");
        }
    }
}
#endif
