using UnityEngine;
using UnityEngine.SceneManagement;

namespace FightingGameBase
{
    /// <summary>
    /// ゲーム開始時に自動的に床（地面）および境界壁を生成・管理するクラスです。
    /// 手動で床を配置していないシーンでも、ゲームを実行（プレイ）すると自動で床が作成されます。
    /// </summary>
    public class FloorManager : MonoBehaviour
    {
        private const string FloorName = "Floor";
        private const string GroundName = "Ground";
        private const string LeftWallName = "Left_Invisible_Wall";
        private const string RightWallName = "Right_Invisible_Wall";

        /// <summary>
        /// ゲーム実行開始時（シーン読み込み直後）に自動実行されます。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnGameStart()
        {
            EnsureFloorExists();

            // シーンの遷移時にも床が存在するか確認・自動生成するようハンドラーを登録
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureFloorExists();
        }

        /// <summary>
        /// シーン内に床が存在するか確認し、なければ自動生成します。
        /// 既に "Floor" や "Ground" などの足場が存在する場合は重複生成しません。
        /// </summary>
        public static GameObject EnsureFloorExists()
        {
            // 1. 既存の床オブジェクトの検索 ("Floor" または "Ground")
            GameObject existingFloor = GameObject.Find(FloorName);
            if (existingFloor == null)
            {
                existingFloor = GameObject.Find(GroundName);
            }

            // 2. 名前に見つからない場合、幅広のコライダーを持つ足場オブジェクトがあるか確認
            if (existingFloor == null)
            {
                Collider2D[] allColliders = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
                foreach (var col in allColliders)
                {
                    if (col.isTrigger) continue;
                    if (col.name.Contains("Wall")) continue;
                    if (col.GetComponentInParent<CharacterBase>() != null) continue;

                    // 幅が広く、キャラクターの足元付近に位置する静的コライダーがあれば床とみなす
                    if (col.bounds.size.x >= 10f && col.bounds.max.y <= 0f && col.bounds.min.y >= -6f)
                    {
                        existingFloor = col.gameObject;
                        break;
                    }
                }
            }

            // 画面外への落下・すり抜け防止のため、左右の透明な壁も確認・作成
            EnsureWallsExist();

            if (existingFloor != null)
            {
                return existingFloor;
            }

            // 3. 床オブジェクトの新規作成
            GameObject floor = new GameObject(FloorName);
            floor.transform.position = new Vector3(0f, -2f, 0f);

            // 4. 見た目（SpriteRenderer）の設定
            SpriteRenderer sr = floor.AddComponent<SpriteRenderer>();

            // FullRect メッシュのテクスチャからスプライトを生成（Sprite Tiling/MeshType 警告を完全に回避）
            Texture2D whiteTex = Texture2D.whiteTexture;
            sr.sprite = Sprite.Create(whiteTex, new Rect(0, 0, whiteTex.width, whiteTex.height), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);

            // スタイリッシュなダークグレー（格闘ゲームの床色）
            sr.color = new Color(0.18f, 0.20f, 0.25f, 1f);
            sr.sortingOrder = -1; // キャラクターの後ろに描画

            // 5. 当たり判定（BoxCollider2D）の設定
            BoxCollider2D collider = floor.AddComponent<BoxCollider2D>();
            floor.transform.localScale = new Vector3(25f, 1f, 1f);
            collider.size = new Vector2(1f, 1f);

            Debug.Log("【FloorManager】ゲームスタートを検知し、床 (Floor) を自動作成しました。");
            return floor;
        }

        /// <summary>
        /// 左右の透明な壁（枠外移動防止用コライダー）を確認・作成します。
        /// </summary>
        public static void EnsureWallsExist()
        {
            float limitX = 12.2f;

            if (GameObject.Find(LeftWallName) == null)
            {
                GameObject leftWall = new GameObject(LeftWallName);
                leftWall.transform.position = new Vector3(-limitX, 2f, 0f);
                BoxCollider2D leftCol = leftWall.AddComponent<BoxCollider2D>();
                leftCol.size = new Vector2(1f, 25f);
            }

            if (GameObject.Find(RightWallName) == null)
            {
                GameObject rightWall = new GameObject(RightWallName);
                rightWall.transform.position = new Vector3(limitX, 2f, 0f);
                BoxCollider2D rightCol = rightWall.AddComponent<BoxCollider2D>();
                rightCol.size = new Vector2(1f, 25f);
            }
        }
    }
}
