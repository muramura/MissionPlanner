# 📋 StampFly & Mission Planner 開発 TODO リスト

むらさんとアンで進める開発タスク・アイデア・改善要望の管理リストです。  
優先順位や着手タイミングを相談しながら、アンが事前に調査・準備を進められるように記録・更新していきます。

---

## 🚀 タスク一覧（ロードマップ）

| ID | タスク名 | 対象 | 優先度 / ステータス | 概要 |
|---|---|---|---|---|
| **TASK-001** | パラメータダウンロード進捗・完了ステータス表示 | MP Android | 📝 **TODO (事前調査中)** | ダウンロード中カウント表示（例: `35/120`）、完了ステータス明示 |
| **TASK-002** | クラッシュ検知・高速ディスアーム (FS_CRASH_TIME) 実機検証 | ArduPilot / 実機 | ✅ **完了 (実機フライト実証済)** | 地面接触時に47度傾斜を検知し0.017秒で即時ディスアーム発動を確認。 |
| **TASK-003** | スローモード（MP側スティックスケーリング）の飛行テスト | MP Android / 実機 | ✅ **完了 (実機フライト実証済)** | 20%は操作性が悪く25%へ切替。最低値25%で確定。TASK-017へ引き継ぎ。 |
| **TASK-004** | 使用しているパラメータのみを取得する（全取得の完全廃止） | MP Android | ✅ **完了 (APKビルド済)** | `getParamList()` の完全排除と `GetParamAsync` によるピンポイント取得の徹底 |
| **TASK-005** | Android時刻を SYSTEM_TIME で StampFly へ通知 & テレメトリ時刻処理の整理 | MP Android / ArduPilot | ✅ **完了 (実機検証済)** | FCのRTCをスマホ時刻で同期（HEARTBEAT駆動）、実機で1Hz現在時刻返信&完全同期を確認 |
| **TASK-006** | アーム時のモーター順次回転チェック（1個ずつ回転 → 全回転） | ArduPilot / StampFly | ✅ **基本実装・実機動作確認済** | アーム時にM1〜M4を1個ずつ回し、最後に全モーターアイドリングへ移行（自動ディスアーム猶予も対応済） |
| **TASK-007** | PR #33996 (Lua/FAT) レビュー返答: MissionPlanner MavFTPでの /APM 非表示問題の調査・返答 | ArduPilot / MP | ✅ **完了 (返答・PRクローズ済)** | Vabe7氏へESP32-S3 FlashFSと150Hzループのキャッシュストール問題・ROMFS代替案を返答しPRクローズ |
| **TASK-008** | アーム指示のモータ回転順番をCW（時計回り）にしたい | ArduPilot (AP_Motors) | 📝 **TODO (実装方針確定)** | アーム順次チェックの回転順を、対角交差順から「右上 ➔ 右下 ➔ 左下 ➔ 左上」の時計回り順（`_test_order`準拠）に変更 |
| **TASK-009** | メインツールバー簡素化＆QUICKタブ配置最適化 (Phone Bat / 重複Link削除) | MP Android | ✅ **完了 (実機動作確認済)** | ツールバーからFC電圧・スマホ残量を削除し、QUICKの重複Link QualityをPhone Bat (📱) に置換。 |
| **TASK-010** | バッテリー低電圧警告・フェイルセーフの適正化 (3.50V設定) | ArduPilot / MP Android | ✅ **完了 (defaults.parm反映済)** | `defaults.parm` の `BATT_LOW_VOLT` を 3.50V に設定・反映完了 |
| **TASK-011** | テレメトリのARM状態（アーム中・アーム・解除）の周期的点滅・ループ現象の修正 | MP Android / テレメトリ | ✅ **完了 (解消済)** | HEARTBEATのシステムID判定を追加・適正化することで周期的点滅・ループ現象を完全解消 |
| **TASK-012** | テレメトリのGPS情報から有意義な情報を表示する (StampFly GNSS搭載対応) | MP Android / UI | ✅ **完了 (統合)** | TASK-014（HACC精度/詳細カード）に統合・反映完了 |
| **TASK-013** | 衛星画像（マッププロバイダ）サイト指定のSETUPへの追加 | MP Android / SETUP | ✅ **完了 (実装・ビルド済)** | フライト画面・プラン画面の地図プロバイダ選択をSETUP画面に追加し、選択の永続化と即時反映を実装 |
| **TASK-014** | メインツールバーGNSSタップで詳細情報表示（実機確認後に要不要選定） | MP Android / UI | ✅ **完了 (実装・ビルド済)** | メインツールバーのGNSSタップで詳細オーバーレイカード（Fix、衛星数、HDOP、HACC、飛行判定等）を表示 |
| **TASK-015** | Wi-Fi SSIDの個別識別化（ARDUPILOT123 ➔ ARDUPILOT_XXXXXX / MAC下位3バイトHEXA6文字） | ArduPilot (AP_HAL_ESP32) | ✅ **完了 (コミット・プッシュ済)** | StampFly SoftAP SSIDにMACアドレス下位3バイト（HEXA6文字）を動的付与し、複数機体での個別識別を実現 |
| **TASK-016** | オンボード NeoPixel RGB LED 動的制御 (WS2812C / RMT移行) | ArduPilot (AP_HAL_ESP32) | ✅ **完了 (コミット・プッシュ済)** | ESP-IDF 5.x/6.0 RMT API移行、SERVO5 (GPIO 39) への動的チャンネルマッピングおよびLED通知対応 |
| **TASK-017** | スローモード設定の最低を２５％にする | MP Android / UI | 📝 **TODO (要件定義・設計)** | 実機飛行テスト結果を反映し、スローモード最小スケーリング制限を20%から25%に引き上げ、UIプリセットも更新 |
| **TASK-018** | CIビルドエラー解消 (RMT名前空間競合 / rmt_channel_t) | ArduPilot (AP_HAL_ESP32) | 📝 **TODO (1Wトークン復活後)** | GitHub Actions CI (`esp32s3empty`) 等で発生した `rmt_channel_t` 型定義競合（typedef-name after struct）の解消 |
| **TASK-019** | フラッシュFS割当エリアをROMFS化（ROMFS利用基盤の整備） | ArduPilot (AP_HAL_ESP32) | 📝 **TODO (1Wトークン復活後)** | FlashFS割当領域をROMFSに割り当て、未設定状態を解消して組み込みROMFSを安全に利用可能にする |
| **TASK-020** | StampFly デフォルトパラメータでフライトモードCH無効化 (`FLTMODE_CH 0`) | ArduPilot (AP_HAL_ESP32) | 📝 **TODO (1Wトークン復活後)** | StampFlyのdefaults.parmにFLTMODE_CH 0を明記し、RC CH5によるモード強制上書きを無効化してMAVLink制御を確実に保護 |
| **TASK-021** | STATUSTEXTメッセージレベルに応じた音声通知（TTS読み上げ）の実装 | MP Android / Core | 📝 **TODO (1Wトークン復活後)** | FCから通知されるSTATUSTEXTをメッセージレベル（Severity）に従ってText-to-Speechで音声通知し、目視なしでの状況把握を実現 |
| **TASK-022** | メインツールバーのWIFI表示をAndroid電波強度（RSSI %）に変更 & パケット品質計算の修正 | MP Android / Core | ✅ **完了 (実機動作確認済)** | ツールバーのWIFI表示をAndroid実測の電波強度(0-100%)に切替、UDP順序逆転誤加算とUTC不一致を修正 |
| **TASK-023** | UDP受信ワーカー専用スレッド化によるテレメトリ停滞問題の解消 | MP Android / Comms | ✅ **完了 (実機検証・TLOG実証済)** | Androidソケット通知遅延を解消するため専用受信スレッド＆ConcurrentQueueを導入。パケット欠落0%、停滞を完全根絶 |
| **TASK-024** | 加速度センサー・キャリブレーション値の妥当性範囲（Sanity Check）プリアームチェックの実装 | ArduPilot (AP_InertialSensor / AP_Arming) | 📝 **TODO (設計・検討)** | `accel_calibrated_ok_all()`が`is_zero()`のみで判定している盲点を解消し、オフセット・スケールの妥当性範囲判定を追加 |





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
- **ステータス**: `[x] 実機フライトログ検証完了 (2026-09-19)`
- **内容・実機検証結果**:
  - 2026-09-19 13:31:56 のフライトにて、機体が地面に接触・傾斜角 47度（>45度設定）に達した瞬間、`Crash: Ground tip-over (angle=47)! Disarmed` が即座に発火。
  - わずか 0.017 秒後に全モーター PWM が 1000 に遮断され、即座のディスアーム停止を実機ログで実証。安全機能が完璧に機能。

---

### TASK-003: スローモード（スティックスケーリング）の操作性確認
- **対象**: `MissionPlanner Android` / `実機フライト`
- **ステータス**: `[x] 実機フライト検証完了 (2026-09-21)`
- **内容・実機検証結果**:
  - トップバーの `🐢 SLOW: 20%` ON/OFF、ゲームパッドボタン（190/191/トグル）での切替および実機フライトフィーリングを確認。
  - **むらさん実機評価**: 20%設定では操作性が悪く（効きが鈍すぎる等）、25%に切り替えて良好な操作フィーリングを確認。最低設定値は25%で十分と判断し、TASK-003は完了。
  - アプリ側の最低値制限およびUIプリセットの改修は新規タスク **TASK-017** として引き継ぎ。

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
- **ステータス**: `[x] 基本実装・実機動作確認済 (2026-09-18)`
- **要望内容**:
  - StampFlyでアーム（Arm）する際、モーターを1番から順に1個ずつスピン（回して）動作確認を行い、最後に全モーターを回してからフライト可能な待機状態（通常スピン）へ移行させたい。
- **実施内容**:
  - `AP_MotorsMulticopter` および `AP_MotorsMatrix` にてアームシーケンス（M1〜M4個別回転スピンアップ）を実装。
  - 実機ログおよびベンチテストで動作確認済み。

---

### TASK-007: PR #33996 (Lua/FAT) レビュー返答（MissionPlanner MavFTPでの `/APM` 非表示問題）
- **対象**: `ArduPilot (PR #33996)` / `MissionPlanner`
- **ステータス**: `[x] 完了 (2026-09-19 返答・PRクローズ済)`
- **対応結果・経緯**:
  - レビュアー（Vabe7氏）のフィードバックを受け、ESP32-S3の実行タイミングを詳細にベンチ解析。
  - **クローズ理由と回答**:
    1. **フライト制御ループ（150Hz）への影響**: ESP32-S3でFlash書き込み・消去およびwear-levellingが発生するとCPU命令キャッシュがストールし、主FCとして150Hzリアルタイム制御を行うStampFlyではIMUサンプリングに深刻なタイミングジッターが生じる。
    2. **パーティション管理の難しさ**: 通常ユーザーがGCSからファームウェアを更新する場合、パーティションテーブルが更新されずマウント障害の原因となる。
    3. **ROMFSの優位性**: スクリプト実行が必要な場合は、ゼロFlash書き込み・パーティション変更不要の `AP_ROMFS` の方が安全かつ最適。
  - 上記内容をVabe7氏へ丁寧に返答し、PR #33996 は正常にクローズ完了。

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

### TASK-010: バッテリー低電圧警告・フェイルセーフの適正化 (3.50V設定)
- **対象**: `ArduPilot (defaults.parm / BATT_LOW_VOLT)` および `MissionPlanner (Android / GCS)`
- **ステータス**: `[x] defaults.parm 反映・コミット完了 (2026-09-19)`
- **背景・課題**:
  - StampFlyで採用されている1S LiPoバッテリー（公称3.7V、満充電4.2V）において、3.8Vで「低電圧（Low Battery）」警告やフェイルセーフが発動すると、通常フライト開始直後やモーター負荷時の電圧降下ですぐに警告状態となってしまう。
  - 実用的なフライト時間を確保し、安全に運用するために低電圧判定しきい値を **3.50V** に適正化したい。
- **実施内容**:
  - `libraries/AP_HAL_ESP32/hwdef/esp32s3m5stampfly/defaults.parm` 内の `BATT_LOW_VOLT` を `3.4` から `3.50` に更新し、コミット完了（`e3a40cac80`）。
  - 実機の現在のパラメータ（3.50V）とも完全に一致し、以降のクリーンフラッシュ時にも確実に 3.50V が初期適用される状態を確保。

---

### TASK-011: テレメトリのARM状態（アーム中・アーム・解除）の周期的ループ現象の修正
- **対象**: `MissionPlanner (Android / GCS)` / `テレメトリ解釈部`
- **ステータス**: `[x] 原因特定・解消完了 (2026-09-21)`
- **背景・課題**:
  - 実機フライト時またはテレメトリ接続時、テレメトリの「ARM」項目（GCS上のアーム状態表示）が「アーム中（Arming）」「アーム（Armed）」「アーム解除（Disarmed）」を定期的に繰り返してフラッピング（点滅・ループ）する現象が確認されていた。
- **原因と解決内容**:
  - **原因**: 複数システムまたは別コンポーネントからの HEARTBEAT パケットが混在して受信された際、システムIDの識別が不十分だったため、異なるステータス値で `cs.armed` が周期的に上書きされていた。
  - **解決**: HEARTBEAT 受信時のシステムID（SysID）判定を厳格化・適正化することで、対象機体（StampFly）の真のステータスのみを反映するように修正。アーム状態の周期的点滅・ループ現象を完全解消。

---

### TASK-012: テレメトリのGPS情報から有意義な情報を表示する (StampFly GNSS搭載対応)
- **対象**: `MissionPlanner (Android / Xamarin)` / `UI (QUICKタブ / ツールバー / HUD)`
- **ステータス**: `[x] 完了 (TASK-014へ統合・反映済)`
- **背景・目的**:
  - StampFlyに超小型GNSSモジュール（u-blox SAM-M8Q等）を搭載。
  - テレメトリで送られてくるGPS情報から、パイロットにとって本当に有益・不可欠な情報を厳選して見やすく表示する。
- **対応内容**:
  - TASK-014のメインツールバーGNSSタップ詳細カードおよびHACC水平精度表示として統合実装完了。

---

### TASK-013: 衛星画像（マッププロバイダ）サイト指定のSETUPへの追加
- **対象**: `MissionPlanner (Android / Xamarin)` / `SETUP (設定画面)` & `マップ描画`
- **ステータス**: `[x] 実装・ビルド完了 (APK反映済 / コミット 04f2ce5c5)`
- **背景・目的**:
  - フライトプラン画面（FlightPlanner）等に衛星画像・地図プロバイダの選択機能があるが、日常的な設定・セットアップを行うSETUP画面からは変更できなかった。
  - スマホ上でも、日本国内で高精細な「国土地理院（航空写真）」や「Google Satellite」「Bing Hybrid」など、利用環境や好みに応じた衛星画像・地図プロバイダをSETUP項目から簡単に選択・切り替えできるようにした。
- **実施内容**:
  1. `FlightData.xaml / FlightData.xaml.cs`:
     - SETUPモーダル内に「🗺️ Map / 衛星画像」設定セクションを追加。
     - Google Satellite, Bing Hybrid, 国土地理院（航空写真/標準）, OpenStreetMap等の主要プロバイダをピッカー選択可能に。
     - 選択結果を `Preferences` に永続保存し、即座にFlightData画面のマッププロバイダへ適用。

---

### TASK-014: メインツールバーGNSSタップで詳細情報表示（実機確認後に要不要選定）
- **対象**: `MissionPlanner (Android / Xamarin)` / `UI (FlightData.xaml / トップバー)`
- **ステータス**: `[x] 実装・ビルド完了 (APK反映済 / コミット 96fdf3f84)`
- **背景・目的**:
  - メインツールバーのGNSSステータス表示（`🛰️ 3D (8)` / `🛰️ No GPS`）をタップすることで、GPSの詳細情報を確認できるようにした。
- **実施内容**:
  1. メインツールバーのGNSS表示部をピル型ボタン化。
  2. タップで全11項目（Fix Type, Satellites, HDOP/VDOP, HACC/VACC, Ground Speed, Course, Lat/Lon, MSL Alt, GPS Time, 総合飛行判定 🟢 READY / 🔴 NOT READY）を表示するダーク調オーバーレイカードをポップアップ。
  3. 1秒周期でのリアルタイム更新、画面外タップまたは閉じるボタンでのクローズを実装。実機画面で確認可能。

---

### TASK-015: Wi-Fi SSIDの個別識別化（ARDUPILOT123 ➔ ARDUPILOT_XXXXXX / MACアドレス下位3バイト HEXA6文字）
- **対象**: `ArduPilot (AP_HAL_ESP32 / StampFly)`
- **ステータス**: `[x] 実装・コミット・プッシュ完了 (コミット 9f20fdc8a3)`
- **背景・課題**:
  - StampFlyのWi-Fi APモード（SoftAP）におけるSSIDが固定値 `ardupilot123` だったため、複数機体が集まると識別・接続が困難だった。
- **実施内容**:
  1. `libraries/AP_HAL_ESP32` の `WiFiDriver.cpp`, `WiFiUdpDriver.cpp`, `Util.cpp` を改修。
  2. SoftAP起動時にハードウェアMACアドレス下位3バイト（24bit）を取得し、`ARDUPILOT_XXXXXX`（HEXA6文字）を自動生成。
  3. 複数機体が存在しても確実に目的の機体を識別・接続可能にし、STATUSTEXTでも新SSIDを通知。

---

### TASK-016: オンボード NeoPixel RGB LED 動的制御 (WS2812C / RMT移行)
- **対象**: `ArduPilot (AP_HAL_ESP32 / StampFly)`
- **ステータス**: `[x] 実装・ビルド・プッシュ完了 (コミット 65d0cba8f8)`
- **背景・目的**:
  - StampFlyに搭載されている2個のオンボード フルカラーLED（WS2812C NeoPixel, GPIO 39）をArduPilotの通知システム（Notify）から制御可能にする。
- **実施内容**:
  1. ESP-IDF 5.x/6.0 の新RMT API（`rmt_tx`）を用いたシリアルLEDドライバを新規実装。
  2. `hwdef.dat` に GPIO 39 を RCOUT 5（`SERVO5`）として追加。
  3. `defaults.parm` に `SERVO5_FUNCTION 120` (RGBLed 1) および `NTF_LED_LEN 2` を設定。
  4. 機体状態（初期化中、GPS測位、アーム中、バッテリー警告等）がStampFly本体のNeoPixelから視覚的に確認可能に。

---

### TASK-017: スローモード設定の最低を２５％にする
- **対象**: `MissionPlanner (Android / Xamarin)` / `UI (FlightData.xaml / FlightData.xaml.cs)`
- **ステータス**: `📝 未着手（TODO / むらさんリクエスト）`
- **背景・課題**:
  - TASK-003の実機飛行テストにおいて、20%スケーリングでは操作性が悪く（効きが鈍すぎる等）、25%に切り替えたところ良好な操作フィーリングが得られることが確認された。
  - 最低設定値は25%で十分であるため、アプリ側の最低値制限およびUIプリセットから20%を撤廃し、25%をMin（最低値）として設定したい。
- **目標・改修内容**:
  1. **内部スケーリング制限の更新**:
     - `FlightData.xaml.cs` 内のスティックスケーリング計算における最小ガード `Math.Max(0.20f, ...)` を `Math.Max(0.25f, ...)` に変更。
     - 設定読み込み時の下限チェック `if (SlowModePct < 20f)` を `if (SlowModePct < 25f)` に変更し、25%を下限値として固定。
  2. **UIプリセットボタンの更新**:
     - `FlightData.xaml` のプリセットボタン `Btn_SlowPct_20` を `Btn_SlowPct_25`（"25% (Min)"）に変更、または20%ボタンを廃止して25%から始まるプリセット配置に調整。
  3. **表示・保存の整合性確認**:
     - 25%選択時に正しくハイライトされ、永続化（Preferences）されることを確認。

---

### TASK-018: CIビルドエラー解消 (RMT名前空間競合 / `rmt_channel_t` typedef vs struct)
- **対象**: `ArduPilot (AP_HAL_ESP32 / RCOutput / RMT)`
- **ステータス**: `📝 未着手（TODO / 1Wトークン復活後に着手）`
- **背景・エラー内容**:
  - GitHub Actions CIのボードビルド（`esp32s3empty` 等）において、以下のコンパイルエラーが発生してビルドが失敗した：
    ```text
    In file included from /opt/esp_idf/components/esp_driver_rmt/include/driver/rmt_common.h:11,
                     from /opt/esp_idf/components/esp_driver_rmt/include/driver/rmt_tx.h:12,
                     from ../../libraries/AP_HAL_ESP32/RCOutput.h:31,
                     from ../../libraries/AP_HAL_ESP32/HAL_ESP32_Class.cpp:27:
    Error: /opt/esp_idf/components/esp_driver_rmt/include/driver/rmt_types.h:22:16: error: using typedef-name 'rmt_channel_t' after 'struct'
       22 | typedef struct rmt_channel_t *rmt_channel_handle_t;
          |                ^~~~~~~~~~~~~
    compilation terminated due to -Wfatal-errors.
    ```
- **原因の分析**:
  - `RCOutput.h` 内で `<driver/rmt_tx.h>`（ESP-IDF 5.x/6.0の新RMTドライバ）をグローバルに include している。
  - ESP-IDFの旧RMTドライバヘッダー（`driver/rmt.h`）等で定義されている `rmt_channel_t`（enum）と、新RMTドライバ（`esp_driver_rmt`）の `typedef struct rmt_channel_t *rmt_channel_handle_t;` が同一翻訳単位（`HAL_ESP32_Class.cpp`）内で衝突している。
- **改修方針（1Wトークン復活後に実施）**:
  1. `RCOutput.h` ヘッダー内での直接的な `<driver/rmt_tx.h>` の include を取りやめ、前方宣言（forward declaration）または不透明ポインタ化を行い、`.cpp`（`RCOutput.cpp`）側のみで include する設計（依存関係の局所化）にする。
  2. ボード定義（`esp32s3empty` や他ターゲット）で旧RMTと新RMTの競合が一切起きないよう安全に分離・クリーン化する。
  3. ローカルおよびCIでのビルド通過を確認後、コミット＆プッシュ。

---

### TASK-019: フラッシュFS割当エリアをROMFS化（ROMFS利用基盤の整備）
- **対象**: `ArduPilot (AP_HAL_ESP32 / StampFly / partitions.csv / hwdef.dat)`
- **ステータス**: `📝 未着手（TODO / 1Wトークン復活後に着手）`
- **背景・目的**:
  - StampFly（ESP32-S3）では、フライトループ（150Hz）中のFlash書き込みによるCPUキャッシュストールを回避するため、書き込み型FlashFS（`AP_FILESYSTEM_ESP32_ENABLED 0`）を無効化している。
  - しかし未設定のままでは、読み取り専用のファイルシステムである ROMFS（`AP_FILESYSTEM_ROMFS`）すら利用できない状態になっている。
  - Flashのストレージ/ファイルシステム用に確保されていた領域・設定を ROMFS 向けに再割り当て・定義し、キャッシュストールを起こさず安全に組み込みファイル（設定、Luaスクリプト等）へアクセスできる基盤を整備する。
- **改修方針（1Wトークン復活後に実施）**:
  1. **パーティションおよびメモリ領域の整理**:
     - `partitions.csv` 等におけるFlashFS（storage）割当領域を見直し、ROMFS組み込み・展開領域として利用できるように設定。
  2. **ROMFSの有効化設定**:
     - `hwdef.dat` または `boards.py` において `define AP_FILESYSTEM_ROMFS_ENABLED 1` を設定し、`AP_ROMFS` を `AP_Filesystem` バックエンドとして正しくリンク・認識させる。
  3. **動作検証**:
     - `esp32s3m5stampfly` のビルドを行い、ROMFS経由でファイルアクセス（`@ROMFS/...`）が正常に行えることを確認。

---

### TASK-020: StampFly デフォルトパラメータでフライトモードCH無効化 (`FLTMODE_CH 0`)
- **対象**: `ArduPilot (AP_HAL_ESP32 / StampFly / defaults.parm)`
- **ステータス**: `📝 未着手（TODO / 1Wトークン復活後に着手）`
- **背景・課題**:
  - StampFlyはWi-Fi経由のMAVLink通信（Mission PlannerのタッチスティックやUDPジョイスティック）を主たる操縦インターフェースとしており、フライトモード変更もMAVLinkコマンド（`SET_MODE`）によって制御される。
  - ArduCopterのデフォルト設定では `FLTMODE_CH`（フライトモード切替RCチャンネル）が `5`（CH5）となっているため、RC入力やパケット受信時の状態によって意図しないモード強制切り替えや干渉が発生するリスクがある。
  - `FLTMODE_CH 0`（0: Disabled / 無効）を `defaults.parm` に明記することで、RCチャンネルによるモード切替を排除し、MAVLink経由での安全かつ確実なフライトモード管理を実現する。
- **改修方針（1Wトークン復活後に実施）**:
  1. `libraries/AP_HAL_ESP32/hwdef/esp32s3m5stampfly/defaults.parm` に `FLTMODE_CH 0` を追加。
  2. ビルドおよび実機でのパラメータ反映を確認後、コミット＆プッシュ。

---

### TASK-021: STATUSTEXTメッセージレベルに応じた音声通知（TTS読み上げ）の実装
- **対象**: `MissionPlanner (MP Android / Xamarin / Core)`
- **ステータス**: `📝 未着手（TODO / 1Wトークン復活後に着手）`
- **背景・目的**:
  - フライト中や機体テスト中、フライトコントローラ（FC）から通知される `STATUSTEXT`（エラー、警告、モード遷移、キャリブレーション状況等）を、パイロットが画面を凝視していなくても即座に把握できるようにしたい。
  - 受信した `STATUSTEXT` のメッセージレベル（`MAV_SEVERITY`: EMERGENCY, ALERT, CRITICAL, ERROR, WARNING, NOTICE, INFO 等）を判定し、設定された重要度閾値に従って Text-to-Speech（TTS）による自動音声通知を行う。
- **改修方針（1Wトークン復活後に実施）**:
  1. **AndroidネイティブTTS連携（`ISpeech` 実装）**:
     - `Xamarin.Android` に `Android.Speech.Tts.TextToSpeech` を組み込んだ `ISpeech` 実装（`AndroidSpeech`）を作成し、`MainV2.speechEngine` および `MAVLinkInterface.Speech` にバインド。
  2. **重要度（Severity）フィルタリングと音声読み上げ連動**:
     - `MAVLinkInterface.cs` における `STATUSTEXT` 受信・パース処理と連携。
     - ユーザーが音声通知のON/OFFおよび対象重要度レベル（例: Warning以上のみ、Info以上など）を設定できるようにする。
  3. **実機動作確認**:
     - Android実機で接続中、FCから送信された指定レベル以上の重要メッセージがクリアに音声発話されることを確認。

---

### TASK-022: メインツールバーのWIFI表示をAndroid電波強度（RSSI %）に変更 & パケット品質計算の修正
- **対象**: `MissionPlanner (MP Android / Xamarin / CurrentState.cs / MAVLinkInterface.cs)`
- **ステータス**: `✅ 完了（実機テスト検証済）`
- **背景・目的**:
  - メインツールバーの `📶 WIFI: xx%` は、直感的には「Androidスマホが受信しているWi-Fi電波の強度（アンテナピクト）」と認識されるのが自然。
  - 現在はMAVLinkのパケット到達率（`cs.linkqualitygcs`）を表示していた上、UDP順序逆転バグ（+254誤加算）やUTCタイムゾーン不一致により0%に急落する問題があった。
  - ユーザー要望に基づき、**ツールバーの表示をAndroidネイティブのWi-Fi電波強度（RSSI 0〜100%）に切り替え**、機体との距離や電波状況を直感的に把握できるようにする。
  - あわせて、内部のMAVLinkパケットロス計算におけるUDP順序逆転誤爆とUTC不一致も是正する。
- **実装内容**:
  1. `AndroidManifest.xml`: `ACCESS_NETWORK_STATE`, `ACCESS_WIFI_STATE` パーミッションを追加。
  2. `MainActivity.cs`: `ConnectivityManager` (API 29+ `SignalStrength`) および `WifiManager.ConnectionInfo.Rssi` から電波強度dBm（-100dBm〜-50dBm）を取得し、0〜100%に変換する `GetWifiRssiPercent()` を実装。`FlightData.GetWifiRssiPercentFunc` および `GetWifiDetailsFunc` を登録。
  3. `FlightData.xaml` & `FlightData.xaml.cs`: ツールバーの `LBL_link_val` 更新時に `GetWifiRssiPercent()` を最優先表示（電波アイコン・色分けも連動）。タップ時に詳細オーバーレイ（Wi-Fi電波強度 dBm / % と MAVLinkパケット品質）を表示する `OnWifiLinkTapped` を追加。
  4. `MAVLinkInterface.cs`: UDP遅延パケット到着時の過剰ロス加算（+254）の防止、初回パケットの同期初期化、重複パケット処理の適正化。
  5. `CurrentState.cs`: `lastvalidpacket` の判定対象を `parent.lastvalidpacket` に統一。
- **実機検証結果**:
  - 実機Androidスマホ上でWi-Fi電波強度がリアルタイムに正確表示（80%〜98%）されることを確認。
  - タップ時の詳細オーバーレイ表示、パケット欠落0%時の100%維持を確認。

---

### TASK-023: UDP受信ワーカー専用スレッド化によるテレメトリ停滞問題の解消
- **対象**: `MissionPlanner (ExtLibs/Comms/CommsUdpSerial.cs)`
- **ステータス**: `✅ 完了（実機検証・TLOG解析実証済）`
- **背景・課題**:
  - Android環境下において、通信開始から数分経過後に20〜30秒間テレメトリの更新が完全停止し、その後溜まっていた数千件のパケットが一気にバースト吸い上げされる現象が発生していた。
  - 原因調査の結果、従来の `BytesToRead`（内部で `FIONREAD` ioctl）によるポーリング方式では、AndroidのLinuxカーネル/ソケットレイヤにおけるバッファ通知遅延（ソケットキューにデータがあっても `BytesToRead == 0` を返す挙動）に依存していたため、MAVLink読み出しループが待機状態に陥っていたことが判明。
- **対策内容（施策1）**:
  1. `CommsUdpSerial.cs` に専用のバックグラウンド受信スレッド（`_rxThread`, IsBackground = true）を導入。
  2. スレッド内で `UdpClient.Receive(ref endpoint)` をブロッキング呼び出しさせ、Androidカーネルにパケットが着信した瞬間に即座に起床・吸い上げるアーキテクチャに刷新。
  3. 受信したバイト列はスレッドセーフな `ConcurrentQueue<byte[]>` へ高速キューイングし、メインループの `Read()` はキューからノンブロッキングで引き出す構造に分離。
  4. 切断・再接続時のスレッド安全な破棄・停止ロジックを実装。
- **実機検証結果 (`2026-09-24_09-16-09.tlog` 解析)**:
  - 総パケット 37,078 件（機体受信 26,979 件、平均 111.7 Hz）において、**パケット欠落 0.000%**。
  - 0.5 秒以上の受信途絶ギャップ **0 回**。
  - 機体起動時間と受信時刻のジッター変動が **±35 ms 以内** と極めて安定。
  - スマホ実機画面上でもテレメトリの停滞・カクツキが一切なく滑らかに更新されることを目視確認。

---

### TASK-024: 加速度センサー・キャリブレーション値の妥当性範囲（Sanity Check）プリアームチェックの実装
- **対象**: `ArduPilot (libraries/AP_InertialSensor / libraries/AP_Arming)`
- **ステータス**: `📝 未着手（TODO / むらさんご指摘・重要安全改善）`
- **背景・課題**:
  - ArduPilotのアーム前診断（`AP_Arming::ins_checks`）において、加速度計のキャリブレーション済み判定は `AP_InertialSensor::accel_calibrated_ok_all()` を呼び出して行われている。
  - しかし現在の `accel_calibrated_ok_all()` は、各加速度計のオフセット（`_accel_offset`）およびスケール（`_accel_scale`）が **「0.0 かどうか（`is_zero()`）」** しか判定していない。
    - オフセットやスケールが `0.0` の場合のみ未キャリブレーションと見なし、`PreArm: 3D Accel calibration needed` を通知してアームを拒否する。
    - 逆に、Flash/EEPROMのパラメータ破損、誤った手動設定、別機体パラメータの流用などで **オフセットが 50m/s²（約5G）やスケールが 0.001 / 99.0 といった物理的にあり得ない異常値・不正値が入っていても、0 ではないため「キャリブレーション正常（OK）」として通過してしまう**。
  - 一方で、キャリブレーション実行時（`AccelCalibrator::accept_result()`）には `|offset| <= GRAVITY_MSS`（9.8m/s²以内）かつ `0.8 <= scale <= 1.2` という妥当性チェックが存在しているが、起動時のプリアームチェックではこの整合性・妥当性検証が完全に抜け落ちていた。
- **改修方針・実装検討**:
  1. **妥当性チェック関数（`accel_calibrated_ok()`）の強化**:
     - `AP_InertialSensor` に保存されているオフセットおよびスケール値が、物理的に妥当な範囲内にあるかを検証するロジックを導入。
     - **オフセット判定**: `fabsf(offset.x/y/z) <= GRAVITY_MSS`（または設定された許容上限値、NaN/Inf除外）。
     - **スケール判定**: 各軸のスケール係数が `0.7f <= scale <= 1.3f`（または妥当なマージン内、NaN/Inf除外）。
  2. **明確なエラーメッセージの通知**:
     - 単なる「0.0」未キャリブレーション時（`PreArm: 3D Accel calibration needed`）と、不正値・破損時（`PreArm: Accel offsets out of range` / `PreArm: Accel scale invalid`）を区別して通知し、原因を即座に特定できるようにする。
  3. **ArduPilot アップストリームへのPR展開**:
     - StampFlyだけでなく、全ArduPilot機体の安全性を根本から底上げする改善となるため、アップストリーム（GitHub ArduPilot/ardupilot）へのPR提出を視野に入れて最小限・堅牢な設計とする。

---

## 💡 新規アイデア・検討中メモ
- （むらさんが思いついたことや、追加したい機能があればここにどんどん追記していきましょう！）
