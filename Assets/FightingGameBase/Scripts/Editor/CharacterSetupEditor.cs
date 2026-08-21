#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;


namespace FightingGameBase.Editor
{
    [InitializeOnLoad]
    public class CharacterSetupEditor
    {
        static CharacterSetupEditor()
        {
            // Unity Editor の読み込み完了後に自動生成を実行します
            EditorApplication.delayCall += AutoCreateCharacters;
            EditorApplication.delayCall += EnsureGroundHasCollider;
        }

        private static void EnsureGroundHasCollider()
        {
            // シーン内のすべての GameObject から「ground」または「Ground」を検索します
            GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (GameObject go in allObjects)
            {
                if (go.name.Equals("ground", System.StringComparison.OrdinalIgnoreCase) || 
                    go.name.Equals("Ground", System.StringComparison.OrdinalIgnoreCase))
                {
                    BoxCollider2D col = go.GetComponent<BoxCollider2D>();
                    if (col == null)
                    {
                        col = go.AddComponent<BoxCollider2D>();
                        col.size = new Vector2(30f, 1f);
                        Debug.Log($"【自動追加】シーン内の {go.name} に BoxCollider2D（当たり判定）を追加しました！");
                    }
                }
            }
        }

        private static void AutoCreateCharacters()
        {
            // お手本キャラクターの自動生成（存在しない場合のみ）
            string templateFolderPath = "Assets/FightingGameBase/Prefabs";
            string templatePrefabPath = templateFolderPath + "/TemplateCharacter.prefab";
            if (!System.IO.File.Exists(templatePrefabPath))
            {
                CreateTemplateCharacterInternal();
            }

            // ライトセーバーキャラクターの自動生成（存在しない場合のみ）
            string lightsaberFolderPath = "Assets/ライトセーバー";
            string lightsaberPrefabPath = lightsaberFolderPath + "/LightsaberCharacter.prefab";
            if (!System.IO.File.Exists(lightsaberPrefabPath))
            {
                CreateLightsaberCharacterInternal();
            }
        }

        // Unityのメニューバーから手動作成する用のメニュー項目
        [MenuItem("FightingGame/お手本キャラクターを作成する")]
        public static void CreateTemplateCharacter()
        {
            CreateTemplateCharacterInternal();
        }

        [MenuItem("FightingGame/ライトセーバーキャラクターを作成する")]
        public static void CreateLightsaberCharacter()
        {
            CreateLightsaberCharacterInternal();
        }

        [MenuItem("FightingGame/地面を作成する")]
        public static void CreateGround()
        {
            // すでに「Ground」という名前のオブジェクトがあれば取得、なければ新規作成
            GameObject ground = GameObject.Find("Ground");
            if (ground == null)
            {
                ground = new GameObject("Ground");
            }
            
            // 位置を設定 (キャラクターの足元になるように少し下に配置します)
            ground.transform.position = new Vector3(0f, -1.5f, 0f);
            
            // 地面用のコライダー（薄く横長に設定）
            BoxCollider2D col = ground.GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = ground.AddComponent<BoxCollider2D>();
            }
            col.size = new Vector2(30f, 1f); // 横幅30, 縦幅1
            
            // 地面のビジュアル用のスプライトレンダラー
            SpriteRenderer sr = ground.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = ground.AddComponent<SpriteRenderer>();
            }
            // 白い四角形スプライトをセット
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(30f, 1f);
            sr.color = new Color(0.3f, 0.3f, 0.3f); // 少し暗いグレーにして地面っぽくする
            
            Selection.activeGameObject = ground;
            Debug.Log("【完了】当たり判定のある地面（Ground）を作成しました！");
        }

        public static void CreateTemplateCharacterInternal()
        {
            // 1. ルート（一番親）となるオブジェクトの作成
            GameObject root = new GameObject("TemplateCharacter");
            
            // 物理エンジンの設定（格ゲーっぽく重力を強めに、回転しないように設定）
            Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f; 
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // 食らい判定用のコライダー（縦長のピル型）
            CapsuleCollider2D hurtCollider = root.AddComponent<CapsuleCollider2D>();
            hurtCollider.size = new Vector2(1f, 2f);

            // 用意したスクリプトをアタッチ
            CharacterBase characterBase = root.AddComponent<CharacterBase>();
            Hurtbox hurtbox = root.AddComponent<Hurtbox>();
            hurtbox.owner = characterBase;
            
            // 入力コントローラーもアタッチ
            PlayerInputController inputController = root.AddComponent<PlayerInputController>();

            // 2. 見た目（グラフィック）とアニメーション用の階層
            GameObject visuals = new GameObject("Visuals");
            visuals.transform.SetParent(root.transform);
            visuals.transform.localPosition = Vector3.zero;
            visuals.transform.localScale = new Vector3(0.35f, 0.35f, 1f); // サイズをさらに小さく設定（0.6 -> 0.35）
            
            SpriteRenderer sr = visuals.AddComponent<SpriteRenderer>(); // 2D画像を表示するためのコンポーネント
            
            string spritePath = "Assets/ライトセーバー/LightsaberCharacterSprite.png";
            Sprite customSprite = null;
            if (System.IO.File.Exists(spritePath))
            {
                customSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            }

            if (customSprite != null)
            {
                sr.sprite = customSprite;
                // 当たり判定とキャラ画像の大きさを同じにする（スケール0.35を考慮）
                hurtCollider.size = new Vector2(customSprite.bounds.size.x * 0.35f, customSprite.bounds.size.y * 0.35f);
            }
            else
            {
                // 見やすいように仮の画像（四角形）をセットし、当たり判定（1x2）と同じサイズにする
                sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                sr.drawMode = SpriteDrawMode.Sliced;
                // スケールが0.35なので、1x2にするためには逆算して大きくしておく
                sr.size = new Vector2(1f / 0.35f, 2f / 0.35f);
                hurtCollider.size = new Vector2(1f, 2f);
            }
            
            // visuals.AddComponent<Animator>();       // スプライト1枚のみにするため、アニメーターは除外
            visuals.AddComponent<FloatingBehavior>(); // ふわふわと浮遊する挙動を追加！

            // 3. 攻撃判定（Hitbox）の階層
            GameObject hitboxObj = new GameObject("PunchHitbox");
            hitboxObj.transform.SetParent(root.transform);
            // 少し前方の、手が出るあたりに仮配置
            hitboxObj.transform.localPosition = new Vector3(0.8f, 0.5f, 0f); 
            
            // 攻撃判定用のコライダー（トリガーにするのがポイント！）
            BoxCollider2D hitCollider = hitboxObj.AddComponent<BoxCollider2D>();
            hitCollider.isTrigger = true;
            hitCollider.size = new Vector2(0.8f, 0.5f);

            // 用意したHitboxスクリプトをアタッチ
            Hitbox hitbox = hitboxObj.AddComponent<Hitbox>();
            hitbox.damage = 10;
            hitbox.ownerPlayerID = 1;

            // 最初は攻撃していないので、非アクティブ（オフ）にしておく
            hitboxObj.SetActive(false);

            // 4. ステータス（CharacterStats）の自動作成とセット
            string folderPath = "Assets/FightingGameBase/Prefabs";
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }

            string statsPath = folderPath + "/TemplateCharacterStats.asset";
            CharacterStats stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(statsPath);
            if (stats == null)
            {
                stats = ScriptableObject.CreateInstance<CharacterStats>();
                AssetDatabase.CreateAsset(stats, statsPath);
                AssetDatabase.SaveAssets();
            }
            characterBase.stats = stats;

            // 5. 自動でプレハブ化して保存する
            string prefabPath = folderPath + "/TemplateCharacter.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction);

            // 作成したオブジェクトを選択状態にする
            Selection.activeGameObject = root;
            
            Debug.Log($"【完了】お手本キャラクターを全部込々にしてプレハブとして作成しました！\n場所: {prefabPath} に保存されています。");
        }

        public static void CreateLightsaberCharacterInternal()
        {
            // 1. ルート（一番親）となるオブジェクトの作成
            GameObject root = new GameObject("LightsaberCharacter");
            
            // 物理エンジンの設定（格ゲーっぽく重力を強めに、回転しないように設定）
            Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f; 
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // 食らい判定用のコライダー（縦長のピル型）
            CapsuleCollider2D hurtCollider = root.AddComponent<CapsuleCollider2D>();
            hurtCollider.size = new Vector2(1f, 2f);

            // 用意したスクリプトをアタッチ
            LightsaberCharacter characterBase = root.AddComponent<LightsaberCharacter>();
            Hurtbox hurtbox = root.AddComponent<Hurtbox>();
            hurtbox.owner = characterBase;
            
            // 入力コントローラーもアタッチ
            PlayerInputController inputController = root.AddComponent<PlayerInputController>();

            // 2. 見た目（グラフィック）とアニメーション用の階層
            GameObject visuals = new GameObject("Visuals");
            visuals.transform.SetParent(root.transform);
            visuals.transform.localPosition = Vector3.zero;
            visuals.transform.localScale = new Vector3(0.35f, 0.35f, 1f); // サイズをさらに小さく設定（0.6 -> 0.35）
            
            SpriteRenderer sr = visuals.AddComponent<SpriteRenderer>(); // 2D画像を表示するためのコンポーネント
            
            string spritePath = "Assets/ライトセーバー/LightsaberCharacterSprite.png";
            Sprite customSprite = null;
            if (System.IO.File.Exists(spritePath))
            {
                // TextureImporterの設定をSpriteに変更する
                TextureImporter importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.SaveAndReimport();
                }
                customSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            }

            if (customSprite != null)
            {
                sr.sprite = customSprite;
                // 当たり判定とキャラ画像の大きさを同じにする（スケール0.35を考慮）
                hurtCollider.size = new Vector2(customSprite.bounds.size.x * 0.35f, customSprite.bounds.size.y * 0.35f);
            }
            else
            {
                // 見やすいように仮の画像（四角形）をセットし、当たり判定（1x2）と同じサイズにする
                sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                sr.drawMode = SpriteDrawMode.Sliced;
                // スケールが0.35なので、1x2にするためには逆算して大きくしておく
                sr.size = new Vector2(1f / 0.35f, 2f / 0.35f);
                hurtCollider.size = new Vector2(1f, 2f);
            }
            
            // visuals.AddComponent<Animator>();       // スプライト1枚のみにするため、アニメーターは除外
            visuals.AddComponent<FloatingBehavior>(); // ふわふわと浮遊する挙動を追加！

            // 3. 攻撃判定（Hitbox）の階層 (リーチは普通、攻撃の発生も早い)
            GameObject hitboxObj = new GameObject("LightsaberHitbox");
            hitboxObj.transform.SetParent(root.transform);
            // ライトセーバーの刀身全体（根本から剣先まで）をカバーするように配置
            hitboxObj.transform.localPosition = new Vector3(1.4f, 0.5f, 0f); 
            
            // 攻撃判定用のコライダー（トリガーにするのがポイント！）
            BoxCollider2D hitCollider = hitboxObj.AddComponent<BoxCollider2D>();
            hitCollider.isTrigger = true;
            // 横に長くして刀身全体を包み込む
            hitCollider.size = new Vector2(2.6f, 0.8f);

            // 用意したHitboxスクリプトをアタッチ
            Hitbox hitbox = hitboxObj.AddComponent<Hitbox>();
            hitbox.damage = 15; // ライトセーバーの威力は少し高めに設定（テンプレートは10）
            hitbox.ownerPlayerID = 1;

            // 最初は攻撃していないので、非アクティブ（オフ）にしておく
            hitboxObj.SetActive(false);
            
            // 3.5. カウンター攻撃専用の判定（Hitbox）の階層
            GameObject counterHitboxObj = new GameObject("CounterHitbox");
            counterHitboxObj.transform.SetParent(root.transform);
            // カウンターは強力なのでさらに広く、少し高めに配置
            counterHitboxObj.transform.localPosition = new Vector3(1.5f, 0.5f, 0f); 
            
            BoxCollider2D counterHitCollider = counterHitboxObj.AddComponent<BoxCollider2D>();
            counterHitCollider.isTrigger = true;
            counterHitCollider.size = new Vector2(2.5f, 1.5f); // 広範囲！

            Hitbox counterHitbox = counterHitboxObj.AddComponent<Hitbox>();
            counterHitbox.damage = 30; // カウンターは高火力
            counterHitbox.ownerPlayerID = 1;
            counterHitboxObj.SetActive(false);
            
            // スクリプトに紐付け
            characterBase.counterHitbox = counterHitbox;

            // 4. ステータス（CharacterStats）の自動作成とセット
            string folderPath = "Assets/ライトセーバー";
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }

            string statsPath = folderPath + "/LightsaberCharacterStats.asset";
            CharacterStats stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(statsPath);
            if (stats == null)
            {
                stats = ScriptableObject.CreateInstance<CharacterStats>();
                // ライトセーバーを扱う敏捷なキャラ用のパラメータ設定
                stats.maxHP = 120;
                stats.moveSpeed = 6.5f; // 素早く動けるようにスピードをアップ
                stats.jumpForce = 13.5f; // ジャンプも高く
                AssetDatabase.CreateAsset(stats, statsPath);
                AssetDatabase.SaveAssets();
            }
            characterBase.stats = stats;

            // カウンターモーション用スプライトを自動アサイン
            void AssignSprite(string path, System.Action<Sprite> assign)
            {
                TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp != null && imp.textureType != TextureImporterType.Sprite)
                {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.SaveAndReimport();
                }
                Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null) assign(s);
            }
            AssignSprite("Assets/ライトセーバー/LightsaberCharacterSprite.png",  s => characterBase.spriteNormal       = s);
            AssignSprite("Assets/ライトセーバー/LightsaberSprite_CounterStance.png",  s => characterBase.spriteCounterStance  = s);
            AssignSprite("Assets/ライトセーバー/LightsaberSprite_CounterFlash.png",   s => characterBase.spriteCounterFlash   = s);
            AssignSprite("Assets/ライトセーバー/LightsaberSprite_CounterAttack.png",  s => characterBase.spriteCounterAttack  = s);

            // 5. 自動でプレハブ化して保存する
            string prefabPath = folderPath + "/LightsaberCharacter.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction);

            // 作成したオブジェクトを選択状態にする
            Selection.activeGameObject = root;
            
            Debug.Log($"【完了】ライトセーバーキャラクターをプレハブとして作成しました！\n場所: {prefabPath} に保存されています。");
        }
    }
}
#endif
