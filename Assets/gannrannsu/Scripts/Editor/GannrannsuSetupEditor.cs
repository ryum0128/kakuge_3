#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FightingGameBase.Editor
{
    // =================================================================================
    // 【GannrannsuSetupEditor（ガンランスキャラクター作成メニュー）】
    // Unityの上のメニューバー「FightingGame」から、
    // ガンランスキャラクターを一発で作成できるようにするエディタスクリプトです。
    // お手本の CharacterSetupEditor を参考に作りました！
    // =================================================================================
    public class GannrannsuSetupEditor
    {
        [MenuItem("FightingGame/ガンランスキャラクターを作成する")]
        public static void CreateGannrannsuCharacter()
        {
            // ================================================================
            // 1. ルート（一番親）となるオブジェクトの作成
            // ================================================================
            GameObject root = new GameObject("GannrannsuCharacter");

            // 物理エンジンの設定（ガンランスは浮いているので重力を軽くします！）
            Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 1.0f;  // お手本は3fだけど、浮いているので軽めに設定
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // やられ判定用のコライダー（ガンランスは横に少し長め）
            CapsuleCollider2D hurtCollider = root.AddComponent<CapsuleCollider2D>();
            hurtCollider.size = new Vector2(2.0f, 3.0f); // サイズを2.0*3.0に変更

            // ガンランス専用スクリプトをアタッチ
            GannrannsuCharacter gannrannsu = root.AddComponent<GannrannsuCharacter>();
            Hurtbox hurtbox = root.AddComponent<Hurtbox>();
            hurtbox.owner = gannrannsu;

            // ガンランス専用の入力コントローラーをアタッチ
            // ※ お手本の PlayerInputController ではなく、ガンランス固有 of 攻撃に対応した
            //    GannrannsuInputController を使います！
            GannrannsuInputController inputController = root.AddComponent<GannrannsuInputController>();

            // ================================================================
            // 2. 見た目（グラフィック）用の階層
            // ================================================================
            GameObject visuals = new GameObject("Visuals");
            visuals.transform.SetParent(root.transform);
            visuals.transform.localPosition = Vector3.zero;

            // 2D画像を表示するコンポーネント
            SpriteRenderer sr = visuals.AddComponent<SpriteRenderer>();
            
            // 追加したガンランスの画像をロードする
            string spritePath = "Assets/gannrannsu/gunlance_sprite.png";
            Sprite gunlanceSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            
            if (gunlanceSprite != null)
            {
                sr.sprite = gunlanceSprite;
                // やられ判定のコライダーサイズと同じ大きさに画像を合わせます
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2(2.0f, 3.0f);
                sr.color = Color.white; // 画像そのままの色を表示
                sr.flipX = true; // 画像の向きを左右反転する
            }
            else
            {
                // 仮の画像（四角形）をセット。色をガンランスっぽいメタリックグレーにします！
                sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2(2.0f, 3.0f);
                sr.color = new Color(0.6f, 0.65f, 0.7f, 1f); // メタリックグレー
            }

            visuals.AddComponent<Animator>(); // アニメーション制御用

            // ================================================================
            // 3. ランスの見た目（画像がない場合のみ仮の槍を作成）
            // ================================================================
            if (gunlanceSprite == null)
            {
                GameObject lance = new GameObject("Lance");
                lance.transform.SetParent(visuals.transform);
                lance.transform.localPosition = new Vector3(0.7f, 0f, 0f); // 前方に配置

                SpriteRenderer lanceSr = lance.AddComponent<SpriteRenderer>();
                lanceSr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                lanceSr.drawMode = SpriteDrawMode.Sliced;
                lanceSr.size = new Vector2(0.8f, 0.2f); // 細長い槍のような形
                lanceSr.color = new Color(0.8f, 0.85f, 0.9f, 1f); // 明るいシルバー
                lanceSr.sortingOrder = 1; // 本体より前に表示
            }

            // ================================================================
            // 4. 砲撃弾の発射位置（FirePoint）
            // ================================================================
            GameObject firePoint = new GameObject("FirePoint");
            firePoint.transform.SetParent(root.transform);
            firePoint.transform.localPosition = new Vector3(1.2f, 0f, 0f); // ランスの先端

            gannrannsu.firePoint = firePoint.transform;

            // ================================================================
            // 5. 攻撃判定（Hitbox）の階層 ― ランスの突き用
            // ================================================================
            GameObject hitboxObj = new GameObject("LanceHitbox");
            hitboxObj.transform.SetParent(root.transform);
            hitboxObj.transform.localPosition = new Vector3(1.4f, 0f, 0f); // ランスの先

            BoxCollider2D hitCollider = hitboxObj.AddComponent<BoxCollider2D>();
            hitCollider.isTrigger = true;
            hitCollider.size = new Vector2(2.0f, 0.4f); // 突き攻撃用（横に長い）

            Hitbox hitbox = hitboxObj.AddComponent<Hitbox>();
            hitbox.damage = 12;        // 突き攻撃のダメージ
            hitbox.ownerPlayerID = 1;

            hitboxObj.SetActive(false); // 最初は非アクティブ

            // ================================================================
            // 5-2. 竜撃砲専用の攻撃判定（Hitbox）の階層（超広範囲！）
            // ================================================================
            GameObject dbHitboxObj = new GameObject("DragonBlastHitbox");
            dbHitboxObj.transform.SetParent(root.transform);
            // キャラの前方に大きく広がるように位置を設定（X方向に+2.5f、幅5.0f）
            dbHitboxObj.transform.localPosition = new Vector3(2.5f, 0f, 0f); 

            BoxCollider2D dbHitCollider = dbHitboxObj.AddComponent<BoxCollider2D>();
            dbHitCollider.isTrigger = true;
            dbHitCollider.size = new Vector2(5.0f, 1.8f); // 射程5.0f、縦幅1.8fの超巨大サイズ！

            Hitbox dbHitbox = dbHitboxObj.AddComponent<Hitbox>();
            dbHitbox.damage = 40; // 竜撃砲の威力
            dbHitbox.ownerPlayerID = 1;

            dbHitboxObj.SetActive(false); // 初期状態は非アクティブ
            gannrannsu.dragonBlastHitbox = dbHitboxObj; // スクリプトにアタッチ

            // ================================================================
            // 6. 砲撃弾プレハブの作成
            // ================================================================
            string folderPath = "Assets/gannrannsu/Prefabs";
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }

            // 砲撃弾のGameObjectを作成
            GameObject shellObj = new GameObject("GannrannsuShell");
            
            // 砲撃弾のスクリプトをアタッチ
            shellObj.AddComponent<GannrannsuShell>();

            // 見た目（小さいオレンジ色の弾）
            SpriteRenderer shellSr = shellObj.AddComponent<SpriteRenderer>();
            shellSr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            shellSr.drawMode = SpriteDrawMode.Sliced;
            shellSr.size = new Vector2(0.3f, 0.3f);
            shellSr.color = new Color(1f, 0.5f, 0f, 1f); // オレンジ色（砲弾っぽい）

            // プレハブとして保存
            string shellPrefabPath = folderPath + "/GannrannsuShell.prefab";
            GameObject shellPrefab = PrefabUtility.SaveAsPrefabAsset(shellObj, shellPrefabPath);
            Object.DestroyImmediate(shellObj); // シーンから一時オブジェクトを削除

            gannrannsu.shellPrefab = shellPrefab; // 参照をセット

            // ================================================================
            // 7. ステータス（CharacterStats）の作成とセット
            // ================================================================
            string statsPath = folderPath + "/GannrannsuStats.asset";
            CharacterStats stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(statsPath);
            if (stats == null)
            {
                stats = ScriptableObject.CreateInstance<CharacterStats>();
                stats.maxHP = 90;        // 体力は少し低め（浮いてるので当たりにくい代わりに）
                stats.moveSpeed = 4f;    // 移動は少し遅め（重い武器だから）
                stats.jumpForce = 10f;   // ジャンプ力は控えめ（そもそも浮いてるので）
                AssetDatabase.CreateAsset(stats, statsPath);
                AssetDatabase.SaveAssets();
            }
            gannrannsu.stats = stats;

            // ================================================================
            // 8. 自動でプレハブ化して保存する
            // ================================================================
            string prefabPath = folderPath + "/GannrannsuCharacter.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction);

            // 作成したオブジェクトを選択状態にする
            Selection.activeGameObject = root;

            Debug.Log($"【完了】浮遊ガンランスキャラクター「GannrannsuCharacter」を作成しました！\n場所: {prefabPath} に保存されています。\n砲撃弾プレハブ: {shellPrefabPath}\nステータス: {statsPath}");
        }
    }
}
#endif
