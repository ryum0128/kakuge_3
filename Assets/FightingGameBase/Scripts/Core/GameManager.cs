using UnityEngine;

namespace FightingGameBase
{
    // =================================================================================
    // 【GameManager（ゲームの進行役）】
    // 試合の始まりや終わり（勝ち負け）を管理するスクリプトです。
    // =================================================================================
    public class GameManager : MonoBehaviour
    {
        // どこからでもアクセスできる「シングルトン」という便利な仕組みです。
        // GameManager.Instance と書くだけで、他のスクリプトからこのゲームマネージャーを操作できます！
        public static GameManager Instance { get; private set; }

        // ゲームの現在の状態（ステート）を表すリストです
        public enum GameState
        {
            Starting, // 試合開始前（Round 1... の演出中など）
            Playing,  // 試合中（プレイヤーが操作できる）
            GameOver  // 試合終了（どちらかが倒れた後）
        }

        // 今のゲームの状態を保存する変数です。最初は Starting（開始前）になっています。
        public GameState currentState = GameState.Starting;
        
        // 試合中かどうかをすぐに確認できる便利な項目です。
        public bool IsPlaying => currentState == GameState.Playing;

        void Awake()
        {
            // --- シングルトンの設定 ---
            if (Instance == null)
            {
                Instance = this; // 自分が最初の GameManager なら、自分を登録します
            }
            else
            {
                // もし2つ以上の GameManager がいたら、2個目以降は消去します（1つだけにするため）
                Destroy(gameObject);
            }
        }

        void Start()
        {
            // 透明なステージ壁を生成・確認
            EnsureStageBoundaries();

            // HUDManagerを動的に生成して動作させる
            if (FindAnyObjectByType<HUDManager>() == null)
            {
                GameObject hudGo = new GameObject("HUDManager");
                hudGo.AddComponent<HUDManager>();
            }

            // 実際はここで「Round 1... Fight!」などのUI演出を行いますが、
            // 今はテスト用なのですぐに試合を開始します！
            StartBattle();
        }

        private void EnsureStageBoundaries()
        {
            float limitX = 12.2f;

            if (GameObject.Find("Left_Invisible_Wall") == null)
            {
                GameObject leftWall = new GameObject("Left_Invisible_Wall");
                leftWall.transform.position = new Vector3(-limitX, 2f, 0f);
                BoxCollider2D col = leftWall.AddComponent<BoxCollider2D>();
                col.size = new Vector2(1f, 25f);
            }

            if (GameObject.Find("Right_Invisible_Wall") == null)
            {
                GameObject rightWall = new GameObject("Right_Invisible_Wall");
                rightWall.transform.position = new Vector3(limitX, 2f, 0f);
                BoxCollider2D col = rightWall.AddComponent<BoxCollider2D>();
                col.size = new Vector2(1f, 25f);
            }
        }

        // 試合を開始するメソッド
        public void StartBattle()
        {
            currentState = GameState.Playing; // 状態を「試合中」に変更！
            Debug.Log("対戦スタート！");
        }

        // どちらかのキャラクターが倒れた時に呼び出されるメソッド
        public void OnCharacterDied(int deadPlayerID)
        {
            // すでにゲームが終わっていたら、何もしません
            if (currentState != GameState.Playing) return;

            currentState = GameState.GameOver; // 状態を「ゲーム終了」に変更！
            
            // 倒れたのが1Pなら2Pの勝ち、2Pなら1Pの勝ちとして計算します
            int winnerID = deadPlayerID == 1 ? 2 : 1;
            
            Debug.Log($"ゲームセット！ プレイヤー {winnerID} の勝利！");
            
            // 【改造のヒント】
            // 実際はここで「K.O.」の文字を画面に出したり、勝った方の勝ちアニメーションを
            // 再生したりするプログラムをここに追加します！
        }
    }
}
