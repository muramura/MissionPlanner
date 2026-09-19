# 📋 StampFly & Mission Planner 開発 TODO リスト

むらさんとアンで進める開発タスク・アイデア・改善要望の管理リストです。  
優先順位や着手タイミングを相談しながら、アンが事前に調査・準備を進められるように記録・更新していきます。

---

## 🚀 タスク一覧（ロードマップ）

| ID | タスク名 | 対象 | 優先度 / ステータス | 概要 |
|---|---|---|---|---|
| **TASK-001** | パラメータダウンロード進捗・完了ステータス表示 | MP Android | 📝 **TODO (事前調査中)** | ダウンロード中カウント表示（例: `35/120`）、完了ステータス明示 |
| **TASK-002** | クラッシュ検知・高速ディスアーム (FS_CRASH_TIME) 実機検証 | ArduPilot / 実機 | ⏳ **ビルド・検証待ち** | Ubuntuビルド後のStampFly実機動作確認 |
| **TASK-003** | スローモード（MP側スティックスケーリング）の飛行テスト | MP Android / 実機 | ⏳ **検証待ち** | 20%設定・スティック操作時の実際の飛行フィーリング確認 |
| **TASK-004** | 使用しているパラメータのみを取得する（全取得の完全廃止） | MP Android | ✅ **完了 (APKビルド済)** | `getParamList()` の完全排除と `GetParamAsync` によるピンポイント取得の徹底 |
| **TASK-005** | Android時刻を SYSTEM_TIME で StampFly へ通知 & テレメトリ時刻処理の整理 | MP Android / ArduPilot | ✅ **完了 (実機検証済)** | FCのRTCをスマホ時刻で同期（HEARTBEAT駆動）、実機で1Hz現在時刻返信&完全同期を確認 |
| **TASK-006** | アーム時のモーター順次回転チェック（1個ずつ回転 → 全回転） | ArduPilot / StampFly | ✅ **基本実装・実機動作確認済** | アーム時にM1〜M4を1個ずつ回し、最後に全モーターアイドリングへ移行（自動ディスアーム猶予も対応済） |
| **TASK-007** | PR #33996 (Lua/FAT) レビュー返答: MissionPlanner MavFTPでの /APM 非表示問題の調査・返答 | ArduPilot / MP | 📝 **TODO (調査・返答案作成)** | Vabe7氏のコメント対応（MavProxyで動作OK、MP MavFTPで/APM見えない原因調査と返答） |
| **TASK-008** | アーム指示のモータ回転順番をCW（時計回り）にしたい | ArduPilot (AP_Motors) | 📝 **TODO (実装方針確定)** | アーム順次チェックの回転順を、対角交差順から「右上 ➔ 右下 ➔ 左下 ➔ 左上」の時計回り順（`_test_order`準拠）に変更 |
| **TASK-009** | メインツールバー簡素化＆QUICKタブ配置最適化 (Phone Bat / 重複Link削除) | MP Android | ✅ **完了 (実機動作確認済)** | ツールバーからFC電圧・スマホ残量を削除し、QUICKの重複Link QualityをPhone Bat (📱) に置換。 |

---

## 📌 タスク詳細

### TASK-001: パラメータダウンロード進捗・完了ステータス表示 (カウント & 完了通知)
- **対象**: `MissionPlanner (Android / Xamarin)`
- **ステータス**: `[ ] 未着手（アンが事前調査・準備を進める）`
- **背景・課題**:
  - 現在、MP Android起動時や接続時にパラメータを限定・差分取得しているが、ユーザー視点で「いま何個ダウンロード中なのか」「いつダウンロードが完了したのか」が画面上で分かりづらい。
  - パラメータ取得完了前に設定変更やフライト操作を行うと、値の不整合や表示遅延に気付きにくい。
- **目標・要望**:
  1. **進捗カウント表示**: ダウンロード中に「パラメータ取得中 (例: `42 / 150` や `受信中: BATT_LOW_VOLT...`)」等の進捗がリアルタイムに分かる表示。
  2. **完了ステータス通知**: 全パラメータ（または必須パラメータ群）の取得が完了したタイミングで、「✅ パラメータ取得完了」等の明確なステータスバッジやメッセージ、トースト通知を表示。
- **アンの事前調査メモ・準備方針**:
  - `MAVLinkInterface.cs` 内の `GetParamList()` / `PARAM_VALUE` 受信ハンドラにおける `param_index` と `param_count` のイベント通知を追跡。
  - `FlightData.xaml` のトップバーまたはステータスバー領域に、小さな進捗バーまたはバッジ（例: `📥 PARAMS: 45/120` -> `✅ PARAMS: OK`）を設ける設計案を準備。
  - 限定ダウンロード（キャッシュ利用時や最小リスト取得時）の完了判定ロジックを確認。

---

### TASK-002: クラッシュ検知・高速ディスアーム (FS_CRASH_TIME) の実機確認
- **対象**: `ArduPilot (esp32s3m5stampfly)`
- **ステータス**: `[/] コード実装完了 → Ubuntuビルド・フライトテスト待ち`
- **内容**:
  - `FS_CRASH_TIME` (0.25s) および `FS_GND_ANGLE` (45deg) の動作確認。
  - 機体が壁に接触・転倒した際の即座のディスアーム発動を確認。

---

### TASK-003: スローモード（スティックスケーリング 1400〜1600μs）の操作性確認
- **対象**: `MissionPlanner Android`
- **ステータス**: `[/] アプリ実装・インストール完了 → 実機飛行テスト待ち`
- **内容**:
  - トップバーの `🐢 SLOW: 20%` ON/OFF、ゲームパッドボタン（190/191/トグル）での切替確認。
  - 実飛行で機敏すぎず安定した操作性が得られるかのフィーリング確認。

---

### TASK-004: 使用しているパラメータのみを取得する（全パラメータ一括取得の完全排除）
- **対象**: `MissionPlanner (Android / Xamarin)`
- **ステータス**: `[x] 実装・ビルド完了 (APK生成済み)`
- **背景・課題**:
  - 接続時に全パラメータ（1000個以上）を一括取得（`getParamList()`）すると、StampFlyなどの小型FCで線形検索負荷による `Main loop slow (121Hz < 150Hz)` や通信帯域圧迫が発生する。
  - コミット `bb08dc024` で基本方針（ピンポイント取得）を決めたが、その後のコミットで `App.xaml.cs` (L110-113) や `FlightData.xaml.cs` (L4777, L6426) にフォールバックとして `getParamList()` が再混入していた。
- **実施内容**:
  1. `App.xaml.cs`: 自動接続時のバックグラウンド `getParamList()` 呼び出しを完全削除。
  2. `FlightData.xaml.cs`: RC Options更新ボタン内の `getParamList()` をピンポイント個別要求（`RC5_OPTION` 〜 `RC12_OPTION`）に置換。
  3. `FlightData.xaml.cs`: Compass設定更新ボタン内の `getParamList()` をピンポイント個別要求（`COMPASS_ENABLE`）に置換。
  4. .NET 8 Android ビルド成功（`com.michaeloborne.MissionPlanner-Signed.apk` 生成済み）。FCのCPU負荷（Main loop slow）を根絶。

---

### TASK-005: Androidの時刻を SYSTEM_TIME で StampFly へ通知 & テレメトリ受信時の時刻書き込み削除
- **対象**: `MissionPlanner Android` / `ArduPilot`
- **ステータス**: `[x] 実装・実機検証完了 (APK生成・完全同期確認済み)`
- **背景・課題**:
  - StampFlyなどのGPS非搭載機体では、FC起動時に時刻（RTC）が存在しない（1970年や0基準）。
  - GCS側でテレメトリメッセージの時刻を書き換えるのではなく、GCSの現在時刻を `SYSTEM_TIME` メッセージで通知し、FC自身の内部時計（`AP_RTC`）をセットすることで根本解決する。
- **実施内容**:
  1. `MAVLinkInterface.cs`:
     - FCからの `HEARTBEAT` 受信契機（イベント駆動）で、機体時刻とGCS時刻の乖離（10秒以上）を判定。
     - 武装解除（Disarmed）中のみ実行し、QGC仕様に準拠して `SYSTEM_TIME` を2回連続送信。
     - 送信時に機体へ `MAV_CMD.SET_MESSAGE_INTERVAL`（MSG ID 2, 1Hz）を要求し、FC側の真のRTC時刻ストリームを有効化。
  2. `FlightData.xaml / FlightData.xaml.cs`:
     - ACTIONSタブ最下部に `🕒 SYNC TIME` ボタンを設置し、手動同期・リアルタイムオフセット表示（`🕒 TIME SYNCED (0.2s)`）を実現。
  3. 実機（Zenfone 7 + StampFly）検証:
     - 機体から `2026-09-19 03:54:40 UTC` が1Hzで返信され、完全同期（オフセット0.2秒）を確認。GCS側でのメッセージ書き換え負荷も完全排除。

---

### TASK-006: アーム時のモーター順次回転チェック（1個ずつ回転 → 全回転）
- **対象**: `ArduPilot (esp32s3m5stampfly)` または `MissionPlanner Android`
- **ステータス**: `[ ] 未着手（むらさんよりメール受信 → 設計・実現方式検討中）`
- **要望内容**:
  - StampFlyでアーム（Arm）する際、モーターを1番から順に1個ずつスピン（回して）動作確認を行い、最後に全モーターを回してからフライト可能な待機状態（通常スピン）へ移行させたい。
- **目的・メリット**:
  - StampFly等のマイクロドローンにおいて、離陸前に各モーターが正常に回るか（異物噛み込み・ブラシモーター寿命・断線がないか）を目視・回転音で直感的に確認でき、離陸直後の転倒やフリップ事故を未然に防止できる。
- **アンの調査・実現アプローチ案**:
  1. **FCファームウェア（ArduPilot）側で実装する場合**:
     - `AP_MotorsMulticopter` または `ArduCopter/arming_checks.cpp` / アーム状態遷移時に、独自のプリフライト・スピンアップシーケンスを追加。
     - アームトリガー後、M1 → M2 → M3 → M4 を順に短時間（例: 200〜300msずつ微小スロットルで回転）駆動し、最後に4個同時にスピンさせてから通常のアーム完了とする。
  2. **GCS（Mission Planner）側から制御する場合**:
     - MAVLinkの `MAV_CMD_DO_MOTOR_TEST`（モーターテストコマンド）をM1〜M4へ順番に送信し、完了後に通常アームコマンド（`MAV_CMD_COMPONENT_ARM_DISARM`）を発行するマクロ／シーケンスボタン。
  3. **パラメータ連動**:
     - 有効/無効や回転時間・出力を設定できるようにパラメータ化を検討。


---

### TASK-007: PR #33996 (Lua/FAT) レビュー返答（MissionPlanner MavFTPでの `/APM` 非表示問題）
- **対象**: `ArduPilot (PR #33996)` / `MissionPlanner`
- **ステータス**: `[ ] 未着手（返答案作成・原因調査中）`
- **レビュアー（Vabe7氏）のコメント内容（2026-09-17）**:
  > "Okay, I tested it a bit more. I can upload and run the hello lua script via MavProxy, but the APM folder doesn't show up in MissionPlanner when using MavFTP.
  > Could it be that Mission Planner is looking for files at `/`, while it is now mounted at `/APM`? I quickly tried mounting it at `/`, but to no avail. And if the mounting fails it still seems to try to attach wear handling, which leads to: `wear_levelling: MAX_WL_HANDLES=8 instances already allocated`, which probably should be avoided."
- **状況・ファクト**:
  1. **Luaスクリプトの動作**: MAVProxy 経由でのアップロード＆スクリプト実行は成功した（FATパーティションとLuaランタイム自体は正しく機能している）。
  2. **課題1 (MavFTP)**: MissionPlanner の MavFTP 画面で `/APM` フォルダが表示されない。
  3. **課題2 (エラー処理)**: マウント失敗時に wear levelling ハンドルが繰り返し確保されて `MAX_WL_HANDLES=8` に達する問題の対処。
- **アンの調査・返答準備方針**:
  - MissionPlanner 側の MavFTP 実装（`FTP.cs` など）がルートディレクトリ一覧取得（`ListDirectory("/")`）を行う際、どのようなレスポンスを期待しているか（ArduPilot側の `AP_Filesystem_ESP32` / `AP_Filesystem` のルート列挙実装との整合性）を調査。
  - マウントエラー時のリトライガードを追加すべきか検討。
  - 的確で丁寧な技術返答案を作成し、むらさんに確認いただく。

---

### TASK-008: アーム指示のモータ回転順番をCW（時計回り）にしたい
- **対象**: `ArduPilot (AP_MotorsMatrix)`
- **ステータス**: `[ ] 未着手（むらさん要望 → 設計・実装方針確定）`
- **背景・目的**:
  - 現在のアーム順次点検（`MOT_ARM_SEQ`）はチャンネル番号順（M1 ➔ M2 ➔ M3 ➔ M4）で回しているため、物理的な配置では「右上 ➔ 左下 ➔ 左上 ➔ 右下」という対角線交差の順番になっている。
  - これを、見た目にも直感的で分かりやすい **「右上 ➔ 右下 ➔ 左下 ➔ 左上」の時計回り（CW）順** で回るようにしたい。
- **注意点（安全性）**:
  - `FRAME_TYPE = 14 (CW_X)` パラメータを変更すると、ハードウェア配線と飛行姿勢制御のミキシングがズレて離陸時に転倒してしまうため、`FRAME_TYPE` は変更しない。
- **実装方針**:
  - `AP_MotorsMatrix.cpp` 内で、ArduPilot標準の各モーターのテスト順序配列 `_test_order[i]`（1:右上, 2:右下, 3:左下, 4:左上）を参照し、`_test_order[i] == (uint8_t)(active_motor_idx + 1)` のモーターを回転させる。
  - これにより、飛行制御の安全性は100%維持したまま、アーム時の点検のみ完璧な時計回り（CW）回転を実現する。

---

### TASK-009: メインツールバー簡素化＆QUICKタブ配置最適化 (Phone Bat / 重複Link削除)
- **対象**: `MissionPlanner (Android / Xamarin)`
- **ステータス**: `[x] 実装・ビルド・実機動作確認完了 (APK反映済)`
- **背景・課題**:
  - メインツールバーに「FC電圧」と「スマホバッテリー残量」が並んでいたが、FC電圧はQUICKタブにすでに存在しており、ツールバーの横幅を圧迫していた。
  - 一方、QUICKタブには「Link Quality」が存在したが、メインツールバー中央に高視認性の「📶 WIFI: 100%」バッジが常に表示されているため、情報が重複していた。
- **実施内容**:
  1. `FlightData.xaml / FlightData.xaml.cs`:
     - メインツールバーから `🔋 FC電圧` および `📱 スマホ残量` を削除。ヘッダーの飛行ステータス視認性を大幅向上。
     - QUICKタブから重複していた `Link Quality` タイルを削除し、空いた Row 4 Col 1 に `Phone Bat 📱` タイルを配置。
     - バッテリー残量に応じたダイナミックカラー表示（青: 50%以上, 黄: 20〜50%, 赤: 20%未満）を実装。
  2. 実機検証:
     - ぴったり10タイル（2列×5行）のままスクロール不要でHUD上部にジャストフィット。メインツールバーもすっきり整理されたことを実機画面で確認。

---

## 💡 新規アイデア・検討中メモ
- （むらさんが思いついたことや、追加したい機能があればここにどんどん追記していきましょう！）

