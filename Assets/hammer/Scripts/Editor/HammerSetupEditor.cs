#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FightingGameBase.Editor
{
    // =================================================================================
    // 【HammerSetupEditor（ハンマーキャラクター作成エディタ）】
    // Unityのメニューバーから「FightingGame」>「ハンマーキャラクターを作成する」を実行すると、
    // 自動的に必要なコンポーネント、コライダー、Hitbox、ステータスファイルが組み込まれた
    // プレハブ（HammerCharacter.prefab）を作成・セットアップするエディタ拡張スクリプトです。
    // =================================================================================
    public class HammerSetupEditor
    {
        [MenuItem("FightingGame/ハンマーキャラクターを作成する")]
        public static void CreateHammerCharacter()
        {
            // 1. ルート（一番親）となるオブジェクトの作成
            GameObject root = new GameObject("HammerCharacter");
            
            // 物理エンジンの設定（格ゲーっぽく重力を強めに、回転しないように設定）
            Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f; 
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // 食らい判定用のコライダー（ハンマーの画像に合わせて2.0f x 2.0fに変更）
            CapsuleCollider2D hurtCollider = root.AddComponent<CapsuleCollider2D>();
            hurtCollider.size = new Vector2(2.0f, 2.0f);

            // 用意したスクリプトをアタッチ
            HammerCharacter hammerCharacter = root.AddComponent<HammerCharacter>();
            Hurtbox hurtbox = root.AddComponent<Hurtbox>();
            hurtbox.owner = hammerCharacter;
            
            // 入力コントローラーもアタッチ
            HammerInputController inputController = root.AddComponent<HammerInputController>();

            // 2. 見た目（グラフィック）とアニメーション用の階層
            GameObject visuals = new GameObject("Visuals");
            visuals.transform.SetParent(root.transform);
            visuals.transform.localPosition = Vector3.zero;
            
            SpriteRenderer sr = visuals.AddComponent<SpriteRenderer>();
            
            // 追加されたハンマーの画像をロードする
            string spritePath = "Assets/hammer/20250131162534.png";
            Sprite hammerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            
            if (hammerSprite != null)
            {
                sr.sprite = hammerSprite;
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2(2.0f, 2.0f); // ハンマーキャラクターのサイズ（横幅広め）に合わせる
                sr.color = Color.white; // 画像そのままの色を表示
            }
            else
            {
                sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2(1f, 2f);
                sr.color = new Color(0.7f, 0.45f, 0.45f); // 画像が無い場合の仮色（赤茶色）
            }
            
            visuals.AddComponent<Animator>();

            // 3. 通常攻撃判定（HammerHitbox）の階層
            GameObject hitboxObj = new GameObject("HammerHitbox");
            hitboxObj.transform.SetParent(root.transform);
            hitboxObj.transform.localPosition = new Vector3(1.5f, 0.3f, 0f); // 射程を少し前方に伸ばす
            
            BoxCollider2D hitCollider = hitboxObj.AddComponent<BoxCollider2D>();
            hitCollider.isTrigger = true;
            hitCollider.size = new Vector2(1.8f, 1.0f); // 攻撃判定の横幅（射程）を少し長く調整

            Hitbox hitbox = hitboxObj.AddComponent<Hitbox>();
            hitbox.damage = 18;
            hitbox.ownerPlayerID = 1;
            hitboxObj.SetActive(false);

            // 4. 特殊攻撃判定（HammerSpecialHitbox）の階層
            GameObject specialHitboxObj = new GameObject("HammerSpecialHitbox");
            specialHitboxObj.transform.SetParent(root.transform);
            specialHitboxObj.transform.localPosition = new Vector3(1.4f, -0.4f, 0f); // 地面叩きつけ判定
            
            BoxCollider2D specialHitCollider = specialHitboxObj.AddComponent<BoxCollider2D>();
            specialHitCollider.isTrigger = true;
            specialHitCollider.size = new Vector2(1.6f, 1.2f);

            Hitbox specialHitbox = specialHitboxObj.AddComponent<Hitbox>();
            specialHitbox.damage = 28;
            specialHitbox.ownerPlayerID = 1;
            specialHitboxObj.SetActive(false);

            // 4-2. 特殊後方攻撃判定（HammerSpecialBackHitbox）の階層
            GameObject specialBackHitboxObj = new GameObject("HammerSpecialBackHitbox");
            specialBackHitboxObj.transform.SetParent(root.transform);
            specialBackHitboxObj.transform.localPosition = new Vector3(-1.4f, -0.4f, 0f); // 後方地面叩きつけ判定（Xを反転）
            
            BoxCollider2D specialBackHitCollider = specialBackHitboxObj.AddComponent<BoxCollider2D>();
            specialBackHitCollider.isTrigger = true;
            specialBackHitCollider.size = new Vector2(1.6f, 1.2f);

            Hitbox specialBackHitbox = specialBackHitboxObj.AddComponent<Hitbox>();
            specialBackHitbox.damage = 28;
            specialBackHitbox.ownerPlayerID = 1;
            specialBackHitboxObj.SetActive(false);

            // 5. 必殺技判定（HammerUltimateHitbox）の階層
            GameObject ultimateHitboxObj = new GameObject("HammerUltimateHitbox");
            ultimateHitboxObj.transform.SetParent(root.transform);
            ultimateHitboxObj.transform.localPosition = new Vector3(1.8f, 0.2f, 0f); // 超巨大スマッシュ判定
            
            BoxCollider2D ultimateHitCollider = ultimateHitboxObj.AddComponent<BoxCollider2D>();
            ultimateHitCollider.isTrigger = true;
            ultimateHitCollider.size = new Vector2(2.4f, 2.2f);

            Hitbox ultimateHitbox = ultimateHitboxObj.AddComponent<Hitbox>();
            ultimateHitbox.damage = 55;
            ultimateHitbox.ownerPlayerID = 1;
            ultimateHitboxObj.SetActive(false);

            // 6. ステータス（CharacterStats）の自動作成とセット
            string folderPath = "Assets/hammer/Prefabs";
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }

            string statsPath = folderPath + "/HammerStats.asset";
            CharacterStats stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(statsPath);
            if (stats == null)
            {
                stats = ScriptableObject.CreateInstance<CharacterStats>();
                stats.maxHP = 130;     // 重量級：最大HPを高めに
                stats.moveSpeed = 3.8f; // 重量級：歩くスピードは遅め
                stats.jumpForce = 9.5f; // 重量級：ジャンプ力は低め
                AssetDatabase.CreateAsset(stats, statsPath);
                AssetDatabase.SaveAssets();
            }
            hammerCharacter.stats = stats;

            // 7. 自動でプレハブ化して保存する
            string prefabPath = folderPath + "/HammerCharacter.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction);

            // 作成したオブジェクトを選択状態にする
            Selection.activeGameObject = root;
            
            Debug.Log($"【完了】ハンマーキャラクターのセットアップが完了しました！\n場所: {prefabPath} に保存されています。");
        }
    }
}
#endif
