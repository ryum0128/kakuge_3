using UnityEngine;
using System.Collections;

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

        [Header("リスポーン設定")]
        [Tooltip("リスポーンまでの待機時間（秒）")]
        public float respawnDelay = 1.5f;

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

            // キャラクターセレクトマネージャーが存在する場合は、キャラクター選択が完了するまで待機
            // (ただし、すでに StartBattle() が呼ばれて Playing になっている場合は何もしない)
            if (currentState != GameState.Playing)
            {
                if (FindAnyObjectByType<CharacterSelectManager>() != null)
                {
                    currentState = GameState.Starting;
                    Debug.Log("GameManager: キャラクターセレクト完了待機中…");
                }
                else
                {
                    StartBattle();
                }
            }
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

        // 試合をリセットするメソッド（キャラクターセレクトへ戻る際など）
        public void ResetGame()
        {
            StopAllCoroutines();
            currentState = GameState.Starting;
            Debug.Log("GameManager: 状態をリセットしました");
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ResetHUD();
            }
        }

        // 試合を開始するメソッド
        public void StartBattle()
        {
            currentState = GameState.Playing; // 状態を「試合中」に変更！
            Debug.Log("対戦スタート！");
        }

        /// <summary>
        /// タイムアップ時の勝敗判定処理。
        /// ストック数・体力が少ない方を敗北（残機0にしてゲームオーバー）とします。
        /// </summary>
        public void OnTimeUpResult(int winnerID, int loserID)
        {
            if (currentState != GameState.Playing) return;

            currentState = GameState.GameOver;

            if (winnerID == 0)
            {
                Debug.Log("タイムアップ！ 引き分け（DRAW GAME）！");
                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.ShowGameResult(0);
                }
                return;
            }

            CharacterBase loser = FindDeadCharacter(loserID);
            if (loser != null)
            {
                loser.isInvincible = false;
                loser.remainingLives = 0;
                loser.currentHP = 0;
                loser.isDead = true;

                Animator anim = loser.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    anim.SetBool("IsDead", true);
                    anim.SetTrigger("Damage");
                }
            }

            Debug.Log($"タイムアップ！ プレイヤー {winnerID} の勝利！（敗者: P{loserID}）");

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowGameResult(winnerID);
            }
        }

        // どちらかのキャラクターが倒れた時に呼び出されるメソッド
        public void OnCharacterDied(int deadPlayerID)
        {
            // すでにゲームが終わっていたら、何もしません
            if (currentState != GameState.Playing) return;

            // 倒れたキャラクターを見つける
            CharacterBase deadChar = FindDeadCharacter(deadPlayerID);

            // 残りライフがあればリスポーンさせる
            if (deadChar != null && deadChar.remainingLives > 0)
            {
                Debug.Log($"プレイヤー {deadPlayerID} が倒れた！ 残りライフ: {deadChar.remainingLives} — リスポーンします…");
                StartCoroutine(RespawnAfterDelay(deadChar));
                return;
            }

            // ライフが0以下 → 本当のゲームオーバー
            currentState = GameState.GameOver; // 状態を「ゲーム終了」に変更！
            
            // 倒れたのが1Pなら2Pの勝ち、2Pなら1Pの勝ちとして計算します
            int winnerID = deadPlayerID == 1 ? 2 : 1;
            
            Debug.Log($"ゲームセット！ プレイヤー {winnerID} の勝利！");

            // HUDに勝敗結果を表示
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowGameResult(winnerID);
            }
        }

        /// <summary>
        /// 指定したplayerIDのCharacterBaseを探して返す
        /// </summary>
        private CharacterBase FindDeadCharacter(int playerID)
        {
            CharacterBase[] allChars = FindObjectsByType<CharacterBase>(FindObjectsSortMode.None);
            foreach (CharacterBase c in allChars)
            {
                if (c.playerID == playerID) return c;
            }
            return null;
        }

        /// <summary>
        /// 一定時間待ってからリスポーンを実行するコルーチン
        /// </summary>
        private IEnumerator RespawnAfterDelay(CharacterBase character)
        {
            yield return new WaitForSeconds(respawnDelay);
            character.Respawn();

            // HUDにリスポーン通知を表示
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowRespawnNotification(character.playerID, character.remainingLives);
            }
        }
    }
}

