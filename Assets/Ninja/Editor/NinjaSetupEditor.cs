#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using FightingGameBase;

namespace FightingGameBase.Editor
{
    [InitializeOnLoad]
    public class NinjaSetupEditor
    {
        static NinjaSetupEditor()
        {
            EditorApplication.delayCall += AutoCreateNinjaCharacter;
        }

        private static void AutoCreateNinjaCharacter()
        {
            string folderPath = "Assets/Ninja";
            string prefabPath = folderPath + "/NinjaCharacter.prefab";
            if (!File.Exists(prefabPath))
            {
                CreateNinjaCharacterInternal();
            }
        }

        [MenuItem("FightingGame/Ninjaキャラクターを作成する")]
        public static void CreateNinjaCharacter()
        {
            CreateNinjaCharacterInternal();
        }

        public static void CreateNinjaCharacterInternal()
        {
            string folderPath = "Assets/Ninja";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // 1. ルート（一番親）となるオブジェクトの作成
            GameObject root = new GameObject("NinjaCharacter");

            Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.mass = 1000f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CapsuleCollider2D hurtCollider = root.AddComponent<CapsuleCollider2D>();
            hurtCollider.size = new Vector2(1f, 2f);

            NinjaCharacter ninja = root.AddComponent<NinjaCharacter>();

            Hurtbox hurtbox = root.AddComponent<Hurtbox>();
            hurtbox.owner = ninja;

            PlayerInputController inputController = root.AddComponent<PlayerInputController>();

            // 2. 見た目（Visuals）: 漆黒の忍装束＋鮮やかな青の鉢金アクセント
            GameObject visuals = new GameObject("Visuals");
            visuals.transform.SetParent(root.transform, false);
            visuals.transform.localPosition = Vector3.zero;
            visuals.transform.localScale = Vector3.one;

            SpriteRenderer sr = visuals.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(1f, 2f);
            sr.color = new Color(0.12f, 0.14f, 0.22f, 1f); // 忍装束（ダークネイビー・漆黒）

            // 鉢金／スカーフ（シアンブルーのアクセント）
            GameObject headband = new GameObject("Headband");
            headband.transform.SetParent(visuals.transform, false);
            headband.transform.localPosition = new Vector3(0.0f, 0.65f, 0f);
            SpriteRenderer bandSr = headband.AddComponent<SpriteRenderer>();
            bandSr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            bandSr.drawMode = SpriteDrawMode.Sliced;
            bandSr.size = new Vector2(1.02f, 0.28f);
            bandSr.color = new Color(0.1f, 0.75f, 1.0f, 1.0f); // 鮮やかな青
            bandSr.sortingOrder = 2;

            // 3. ブロックシールド（他キャラ共通のパリーシールド）
            GameObject shieldObj = new GameObject("BlockShield");
            shieldObj.transform.SetParent(root.transform, false);
            shieldObj.transform.localPosition = new Vector3(0.7f, 0f, 0f);
            shieldObj.transform.localScale = Vector3.one;

            SpriteRenderer shieldSr = shieldObj.AddComponent<SpriteRenderer>();
            shieldSr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            shieldSr.drawMode = SpriteDrawMode.Sliced;
            shieldSr.size = new Vector2(0.8f, 2.0f);
            shieldSr.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            shieldSr.sortingOrder = 10;
            shieldObj.SetActive(false);

            // 4. 青い球（BlueOrb）通常攻撃オブジェクト
            GameObject orbObj = new GameObject("BlueOrb");
            orbObj.transform.SetParent(root.transform, false);
            orbObj.transform.localPosition = new Vector3(0.95f, 0.15f, 0f);

            Sprite circleSprite = NinjaCharacter.GetOrCreateCircleSprite();

            // 外層（オーラ）
            GameObject auraObj = new GameObject("OrbAura");
            auraObj.transform.SetParent(orbObj.transform, false);
            SpriteRenderer auraSr = auraObj.AddComponent<SpriteRenderer>();
            auraSr.sprite = circleSprite;
            auraSr.color = new Color(0.0f, 0.35f, 1.0f, 0.45f);
            auraSr.sortingOrder = 8;
            auraObj.transform.localScale = Vector3.one * 1.35f;

            // 主体球
            GameObject mainObj = new GameObject("OrbMain");
            mainObj.transform.SetParent(orbObj.transform, false);
            SpriteRenderer mainSr = mainObj.AddComponent<SpriteRenderer>();
            mainSr.sprite = circleSprite;
            mainSr.color = new Color(0.05f, 0.65f, 1.0f, 0.90f);
            mainSr.sortingOrder = 9;
            mainObj.transform.localScale = Vector3.one * 1.0f;

            // 中心核（発光コア）
            GameObject coreObj = new GameObject("OrbCore");
            coreObj.transform.SetParent(orbObj.transform, false);
            SpriteRenderer coreSr = coreObj.AddComponent<SpriteRenderer>();
            coreSr.sprite = circleSprite;
            coreSr.color = new Color(0.88f, 0.98f, 1.0f, 0.95f);
            coreSr.sortingOrder = 10;
            coreObj.transform.localScale = Vector3.one * 0.55f;

            // 当たり判定
            CircleCollider2D orbCol = orbObj.AddComponent<CircleCollider2D>();
            orbCol.isTrigger = true;
            orbCol.radius = 0.65f;
            orbCol.enabled = false;

            Hitbox hitbox = orbObj.AddComponent<Hitbox>();
            hitbox.damage = 25;
            hitbox.ownerPlayerID = 1;
            hitbox.owner = ninja;
            hitbox.isNormalAttack = true;
            hitbox.postureDamageMultiplier = 1.5f;

            orbObj.SetActive(false);

            // 5. ステータス（CharacterStats）の自動作成と設定
            string statsPath = folderPath + "/NinjaStats.asset";
            CharacterStats stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(statsPath);
            if (stats == null)
            {
                stats = ScriptableObject.CreateInstance<CharacterStats>();
                stats.maxHP = 100;
                stats.moveSpeed = 6.2f;  // 忍者らしい敏捷な歩行スピード
                stats.jumpForce = 13.0f; // 軽快なジャンプ力
                AssetDatabase.CreateAsset(stats, statsPath);
                AssetDatabase.SaveAssets();
            }
            ninja.stats = stats;

            // 6. プレハブとして保存
            string prefabPath = folderPath + "/NinjaCharacter.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"【完了】Ninjaキャラクターをプレハブとして作成しました！\n保存場所: {prefabPath}");
        }
    }
}
#endif
