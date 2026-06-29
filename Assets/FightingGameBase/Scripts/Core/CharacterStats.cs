using UnityEngine;

namespace FightingGameBase
{
    // =================================================================================
    // 【CharacterStats（キャラクターステータス）】
    // このスクリプトは、キャラクターの「体力」「スピード」「ジャンプ力」などの
    // データを保存しておくためのものです。
    // ScriptableObject（スクリプタブルオブジェクト）という機能を使っており、
    // プロジェクト内に「データファイル（Asset）」として保存できるようになります！
    // =================================================================================
    
    // [CreateAssetMenu] を書くと、Unityの右クリックメニュー（Create）から
    // このデータファイルを新しく作れるようになります！
    [CreateAssetMenu(fileName = "NewCharacterStats", menuName = "FightingGame/Character Stats")]
    public class CharacterStats : ScriptableObject
    {
        // [Header] は、Inspector（右側の画面）で見出しをつけるための機能です。
        [Header("基本ステータス")]
        
        // [Tooltip] を書くと、Inspectorでマウスを乗せたときに説明文が出ます。
        [Tooltip("キャラクターの最大体力")]
        public int maxHP = 100; // 体力の最大値（整数）

        [Header("移動パラメータ")]
        
        [Tooltip("歩くスピード")]
        public float moveSpeed = 5f; // 歩く速さ（小数も使えるfloat型）
        
        [Tooltip("ジャンプする力")]
        public float jumpForce = 12f; // ジャンプする力（大きいほど高く飛びます）
    }
}
