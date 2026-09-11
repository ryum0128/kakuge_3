#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace FightingGameBase.Editor
{
    public class FlameCharacterSetupEditor
    {
        [MenuItem("FightingGame/炎キャラクターを作成する")]
        public static void CreateFlameCharacter()
        {
            CreateFlameCharacterInternal();
        }

        public static void CreateFlameCharacterInternal()
        {
            string folderPath = "Assets/Flame";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // 1. スプライトのインポート設定を確認
            string spritePath = folderPath + "/FlameCharacterSprite.png";
            EnsureSpriteSettings(spritePath);

            Sprite characterSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

            // 2. ルート（一番親）となるオブジェクトの作成
            GameObject root = new GameObject("FlameCharacter");
            
            Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.mass = 1000f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // 食らい判定（Hurtbox）: TAIKENと同じサイズ (1, 2)
            CapsuleCollider2D hurtCollider = root.AddComponent<CapsuleCollider2D>();
            hurtCollider.size = new Vector2(1f, 2f);

            // キャラクター制御スクリプト
            FlameCharacter flameChar = root.AddComponent<FlameCharacter>();
            Hurtbox hurtbox = root.AddComponent<Hurtbox>();
            hurtbox.owner = flameChar;

            // プレイヤー入力コントローラー
            PlayerInputController inputController = root.AddComponent<PlayerInputController>();

            // 3. 見た目（Visuals）: TAIKENと同じ白い箱
            GameObject visuals = new GameObject("Visuals");
            visuals.transform.SetParent(root.transform);
            visuals.transform.localPosition = Vector3.zero;
            visuals.transform.localScale = Vector3.one;

            SpriteRenderer sr = visuals.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(1f, 2f);
            sr.color = Color.white;

            flameChar.visualsTransform = visuals.transform;

            // 4. 武器: シンプルな赤いボール（TAIKENのWeaponと同様にVisualsの子にして常にキャラと一体化）
            GameObject weaponObj = new GameObject("RedBallWeapon");
            weaponObj.transform.SetParent(visuals.transform);
            weaponObj.transform.localPosition = new Vector3(0.55f, 0.25f, 0f); // 常にキャラの体・手元にぴったり密着
            weaponObj.transform.localScale = new Vector3(1.4f, 1.4f, 1f);

            SpriteRenderer ballSr = weaponObj.AddComponent<SpriteRenderer>();
            ballSr.sprite = FlameCharacter.GetOrCreateCircleSprite();
            ballSr.color = new Color(1.0f, 0.2f, 0.1f, 1.0f);
            ballSr.sortingOrder = 5;

            flameChar.redBallWeapon = weaponObj;

            // 5. ブロックシールド（他キャラクターと完全同一の青いシールド）
            GameObject shieldObj = new GameObject("BlockShield");
            shieldObj.transform.SetParent(root.transform);
            shieldObj.transform.localPosition = new Vector3(0.7f, 0f, 0f);
            shieldObj.transform.localScale = Vector3.one;

            SpriteRenderer shieldSr = shieldObj.AddComponent<SpriteRenderer>();
            shieldSr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            shieldSr.drawMode = SpriteDrawMode.Sliced;
            shieldSr.size = new Vector2(0.8f, 2.0f);
            shieldSr.color = new Color(0.3f, 0.8f, 1f, 0.6f); // 他キャラ同一の鮮やかな青
            shieldSr.sortingOrder = 10;
            shieldObj.SetActive(false);

            flameChar.blockShield = shieldObj;

            // 6. 炎の剣（Fire Blade）通常攻撃用ギミック
            GameObject bladePivot = new GameObject("FireBladePivot");
            bladePivot.transform.SetParent(root.transform);
            bladePivot.transform.localPosition = new Vector3(0.2f, 0.2f, 0f);
            bladePivot.transform.localRotation = Quaternion.identity;
            flameChar.fireBladePivot = bladePivot.transform;

            GameObject bladeObj = new GameObject("FireBlade");
            bladeObj.transform.SetParent(bladePivot.transform);
            bladeObj.transform.localPosition = Vector3.zero;
            bladeObj.transform.localRotation = Quaternion.identity;
            bladeObj.transform.localScale = Vector3.one;

            SpriteRenderer bladeSr = bladeObj.AddComponent<SpriteRenderer>();
            Sprite bladeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(folderPath + "/FireBladeSprite.png");
            if (bladeSprite != null)
            {
                bladeSr.sprite = bladeSprite;
                flameChar.fireBladeSprite = bladeSprite;
            }
            bladeSr.sortingOrder = 8;
            bladeObj.SetActive(false);
            flameChar.fireBladeObj = bladeObj;
            flameChar.fireBladeRenderer = bladeSr;

            // 剣の刃（blade）のグラフィックと完全一致する当たり判定（BladeHitbox）
            GameObject hitboxObj = new GameObject("BladeHitbox");
            hitboxObj.transform.SetParent(bladeObj.transform);
            hitboxObj.transform.localPosition = new Vector3(0.78f, 0.78f, 0f);
            hitboxObj.transform.localRotation = Quaternion.Euler(0, 0, 45f);
            hitboxObj.transform.localScale = Vector3.one;

            BoxCollider2D boxCol = hitboxObj.AddComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector2(2.2f, 0.75f);
            boxCol.offset = Vector2.zero;

            Hitbox hitbox = hitboxObj.AddComponent<Hitbox>();
            hitbox.damage = 18;
            hitbox.ownerPlayerID = 1;
            hitbox.postureDamageMultiplier = 1.5f;
            hitbox.isNormalAttack = true;
            hitboxObj.SetActive(false);

            flameChar.normalAttackHitbox = hitbox;

            // 5. ステータス（CharacterStats）
            string statsPath = folderPath + "/FlameCharacterStats.asset";
            CharacterStats stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(statsPath);
            if (stats == null)
            {
                stats = ScriptableObject.CreateInstance<CharacterStats>();
                stats.maxHP = 100;
                stats.moveSpeed = 6.0f;
                stats.jumpForce = 12.5f;
                AssetDatabase.CreateAsset(stats, statsPath);
                AssetDatabase.SaveAssets();
            }
            flameChar.stats = stats;

            // 6. プレハブとして保存
            string prefabPath = folderPath + "/FlameCharacter.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"【完了】炎キャラクターをプレハブとして作成しました！ 場所: {prefabPath}");
        }

        private static void EnsureSpriteSettings(string path)
        {
            if (File.Exists(path))
            {
                TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp != null)
                {
                    bool changed = false;
                    if (imp.textureType != TextureImporterType.Sprite)
                    {
                        imp.textureType = TextureImporterType.Sprite;
                        imp.spriteImportMode = SpriteImportMode.Single;
                        changed = true;
                    }
                    if (imp.alphaIsTransparency != true)
                    {
                        imp.alphaIsTransparency = true;
                        changed = true;
                    }
                    TextureImporterSettings settings = new TextureImporterSettings();
                    imp.ReadTextureSettings(settings);
                    if (settings.spriteMeshType != SpriteMeshType.FullRect)
                    {
                        settings.spriteMeshType = SpriteMeshType.FullRect;
                        imp.SetTextureSettings(settings);
                        changed = true;
                    }
                    if (changed)
                    {
                        imp.SaveAndReimport();
                    }
                }
            }
        }
    }
}
#endif
