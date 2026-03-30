# BATTLES IN FLOODED GROUNDS

Battles in Flooded Grounds は、Unity 2022 LTS で構築された 3D アクション試作プロジェクトです。  
タイトル画面からゲーム本編へ遷移し、敵 AI・ゴースト会話・透明化（Dissolve）・HUD 表示などを含む構成になっています。

## 開発環境

- Unity: 2022.3.39f1
- 主なパッケージ:
  - TextMeshPro
  - Timeline
  - UGUI
  - Visual Scripting
  - Unity MCP (`jp.shiranui-isuzu.unity-mcp`)
- 音声: Wwise 連携プロジェクト同梱

## プロジェクト構成（主要）

- Unity プロジェクト本体: `Assets/`, `Packages/`, `ProjectSettings/`
- ソリューション: `Flooded Grounds.sln`
- Wwise プロジェクト: `Flooded Grounds_WwiseProject/`
- サウンドバンク出力先: `Assets/StreamingAssets/Audio/GeneratedSoundBanks/`

## シーン構成

Build Settings には以下 3 シーンが登録済みです。

1. `Assets/Flooded_Grounds/Scenes/TitleScene.unity`
2. `Assets/Flooded_Grounds/Scenes/Scene_A.unity`
3. `Assets/Flooded_Grounds/Scenes/OverScene.unity`

遷移の流れ:

- TitleScene: Start で `Scene_A` へ
- Scene_A: プレイヤー HP が 0 になると `OverScene` へ
- OverScene: Restart で `Scene_A` へ

## 実行手順

1. Unity Hub で Unity `2022.3.39f1` をインストール
2. 本フォルダを Unity Hub から開く
3. `TitleScene` を開いて Play

補足:

- 初回インポートは時間がかかる場合があります
- Console に赤エラーがある場合は、import 完了を待って再生してください

## 操作方法（現行実装ベース）

`Scene_A` のプレイヤー（`FpsController` / `CharController_Motor`）操作:

- 移動: 矢印キー（↑↓←→）
- 走る: Shift（左/右）
- ジャンプ: Space（Input Manager の Jump）
- ゴースト会話: A
- 透明化トグル: X

## 主なゲーム要素

- 3人称カメラ追従（`ThirdPersonOrbitCamera`）
- プレイヤー透明化（Dissolve）
- HP ゲージ、被ダメージポップアップ、レーダー HUD
- ゴースト会話 UI
- Skeleton 敵 AI（徘徊 / 索敵 / 追跡 / 攻撃）
- 透明化時に敵がターゲットを見失う挙動

## 主要スクリプト

- プレイヤー制御: `Assets/Flooded_Grounds/Scripts/FPSController/CharController_Motor.cs`
- カメラ制御: `Assets/Flooded_Grounds/Scripts/FPSController/ThirdPersonOrbitCamera.cs`
- 敵 AI: `Assets/Flooded_Grounds/Scripts/Enemies/DungeonSkeletonEnemyAI.cs`
- タイトル画面: `Assets/Flooded_Grounds/Scripts/UI/TitleSceneController.cs`
- ゲームオーバー画面: `Assets/Flooded_Grounds/Scripts/UI/OverSceneController.cs`

## エディタツール

Unity メニュー `Tools/Flooded Grounds` からセットアップ補助を利用できます。

- Setup Iron Juggernaut Third Person
  - プレイヤー外見・3人称カメラ・敵配置の一括設定
- Setup Dungeon Skeleton Enemies
  - Skeleton 敵の再配置と Animator/AI 設定

対応スクリプト:

- `Assets/Flooded_Grounds/Scripts/Editor/ThirdPersonJuggernautSetupTool.cs`
- `Assets/Flooded_Grounds/Scripts/Editor/DungeonSkeletonSetupTool.cs`

## Wwise 連携メモ

- Wwise プロジェクトは `Flooded Grounds_WwiseProject/` に同梱
- Unity 側サウンドバンク配置先は `Assets/StreamingAssets/Audio/GeneratedSoundBanks/Windows` または `Mac`

音が出ない場合の基本確認:

1. 対象プラットフォームの SoundBank が生成済みか
2. `Assets/StreamingAssets/Audio/GeneratedSoundBanks/<Platform>` に必要な `.bnk` が存在するか
3. Unity 再生時に Wwise 初期化エラーが Console に出ていないか

## トラブルシュート

- MissingReference や null 参照が出る:
  - `Scene_A` 上の `FpsController` に `CharacterController` と `CharController_Motor` があるか確認
- カメラが追従しない:
  - `CharController_Motor.cam` と `ThirdPersonOrbitCamera.target` の参照を確認
- 会話が開始しない:
  - ゴーストオブジェクト名（既定: `Little_Ghost_ZOMbi (8)`）と距離/向き条件を確認
- 透明化が効かない:
  - `dissolveFallbackMaterial` が `_DissolveAmount` プロパティを持つか確認

## 注意

- 本リポジトリには含まれていませんが、アセットストア由来を含む複数アセットを利用しています。
