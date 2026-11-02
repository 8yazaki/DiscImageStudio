<div align="center">

<p align="center"><a href="README.md">简体中文</a> · <a href="README.en.md">English</a> · <b>日本語</b></p>

![Banner](docs/images/DiscImageStudio_Banner_2.png)

<h2>ディスク表面に見える画像を生成して書き込む、オールインワンツール</h2>

Disc Image Studio は、ディスク表面用の画像を生成して書き込む Windows 用ツールです。**どんな**書き込みドライブでも、**どんな** DVD / CD / Blu-ray ディスクの表面にも、肉眼で見える絵柄を刻めます。

![.NET バージョン](https://img.shields.io/badge/.NET-10.0-512bd4?style=flat-square)
![対応プラットフォーム](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078d6?style=flat-square)
![開発言語](https://img.shields.io/badge/Language-C%23-68217A?style=flat-square)
![GitHub Repo size](https://img.shields.io/github/repo-size/JiaFeiMiao-K-Cat/DiscImageStudio?style=flat-square&color=3cb371)
![PR 歓迎](https://img.shields.io/badge/PRs-Welcome-brightgreen?style=flat-square)
[![ライセンス：GPL-3.0-or-later](https://img.shields.io/badge/License-GPL--3.0--or--later-blue?style=flat-square)](LICENSE)

</div>

> これは[中国語の原文 README](README.md) の日本語訳です。内容に食い違いがある場合は中国語版が正となります。`docs/` 以下の技術文書は現在中国語版のみで、スクリーンショットも中国語表示の画面です。

---

## 仕上がりの例

![](docs/images/case1.jpg)

![](docs/images/case2.jpg)

---

## 概要

Disc Image Studio は、1 枚の画像を CD・DVD・単層 BD-R の物理層の制約に合ったデータへ変換します。書き込むと、ディスクの表面に肉眼で見える絵柄が現れます。

## 画面プレビュー

![ホーム：テンプレートと最近のプロジェクト](docs/images/ui-home.png)

![デザイン画面：レイヤーキャンバスとトーン](docs/images/ui-design.png)

![エクスポート画面：ライトテーマとイメージ出力](docs/images/ui-export.png)

---

## クイックスタート

[GitHub Releases](https://github.com/JiaFeiMiao-K-Cat/DiscImageStudio/releases) からリリース版の EXE をダウンロードし、展開してそのまま実行します。EXE には .NET ランタイムが含まれています。

### 1. プロジェクトを作成する

ホーム画面で CD・DVD・Blu-ray のいずれかを選び、出力先を指定してプロジェクトを作成します。

### 2. ディスク面をデザインする

デザイン画面で画像を取り込み、レイヤーでレイアウトを整えます。右側の「トーン」で、ディザ・ガンマ・明るさ・階調レベルを調整します。

### 3. プレビューとエクスポート

プレビュー画面でディスク面への投影を確認します。エクスポート画面で ISO / RAW / WAV+CUE を生成するか、即時書き込みで未使用のディスクへ直接書き込みます。

即時書き込みは実験的な機能です。使えなくなっても構わないテスト用メディアだけを使ってください。

## 機能

### ディスクデータの生成

- **CD 対応**：どんな CD でも使えます。正しいヘッダー付きの WAV トラックと対応する CUE を書き出せ、cdrecord や ImgBurn などの書き込みソフトで使えます。RAW トラックの書き出しにも対応します。詳しくは [CD 書き込み互換性の説明](docs/CD_AUDIO_COMPATIBILITY.md)（中国語）を参照してください。
- **DVD 対応**：どんな DVD でも使えます。ISO イメージを書き出せます。
- **Blu-ray 対応**：単層 25 GB の BD-R に対応し、描画のみ、または UDF 2.01 ファイル付きにできます。詳しくは [ADDING_BLURAY.md](docs/ADDING_BLURAY.md)（中国語）を参照してください。
- **ディスク内ファイル**：CD／DVD／Blu-ray では、デザイン画面で保存したいフォルダーと内周／外周の位置を直接選べ、エクスポート画面でも調整できます。設定はプロジェクトに保存されます。
- **ファイル領域のプレビュー**：ハイブリッドディスクのデザイン投影と完成品の読み戻しでは、ファイル領域（ファイルシステムを含む）が黄色で示されます。
- **メディアの階調方式**：CD と DVD はどちらも白黒 2 階調で、CD は `0D / AA`、DVD のスクランブル領域は `A5 / 92` を使います。詳しくは [PHYSICAL_MODEL_AUDIT.md](docs/PHYSICAL_MODEL_AUDIT.md)（中国語）を参照してください。
- **階調ディザツール**：独立したプログラムで、元画像を `0/255` の白黒 2 階調 PNG に変換することもできます。画像を開く／ドラッグして取り込む、並べてプレビュー、劣化のない PNG 書き出しに対応します。詳しくは[使い方とビルドの説明](tools/FourLevelDither/README.md)を参照してください。
- **ディスクプリセット**：複数ブランドの CD／DVD／BD のパラメーターを内蔵しています。ご自身で校正したデータを提供することもできます。詳しくは[プリセット管理の説明](docs/DISC_PRESETS.md)（中国語）を参照してください。

### 補助校正

Disc Image Studio には、賢く正確な補助校正の手順があります。目視での校正に比べ、補助校正のアルゴリズムは、求められるディスクの物理データの精度を 10<sup>-6</sup> から 10<sup>-14</sup> へ高めます。校正前（一定のトラックピッチ）では放射状の十字線が目立って曲がりますが、補助校正で得た 3 次のトラックピッチのパラメーターで生成し直して書き込むと、線がより放射状の直線に近づきます。

| 校正前 · 一定のトラックピッチ | 校正後 · 3 次トラックピッチ |
| --- | --- |
| ![校正前の DVD 実写：放射状の十字線が大きく曲がっている](docs/images/verbatim-dvd-calibration-before.jpg) | ![校正後の DVD 実写：放射状の十字線がより直線に近い](docs/images/verbatim-dvd-calibration-after.jpg) |
| `P20261004-133519.jpg` | `P20261004-135416.jpg` |

手順とパラメーターは[補助校正の説明](docs/CALIBRATION.md)（中国語）を参照してください。

### 書き込み

- **イメージファイルの出力**：完全な ISO / RAW ファイルを書き出し、適切な書き込みソフトで手動で書き込めます。
- **即時ストリーミング書き込み（実験的）**：CD・DVD・BD-R を、完全なイメージを保存せずに生成しながら書き込みます。BD-R は MMC のシーケンシャル書き込みを使います。
- **未使用ではないメディアは絶対に使わないでください！**

### アプリの形態

- 2 つの入り口：引数なしで起動すると GUI が開き、引数を付けると統一コマンドラインとして動作します。
- モジュール構成：CD・DVD・Blu-ray は `IOpticalDiscModule` を通じて接続され、コマンドカタログとタスクの契約を共有します。詳しくは[アーキテクチャの説明](docs/ARCHITECTURE.md)（中国語）を参照してください。

## 表示言語

画面は **簡体字中国語 / English / 日本語** に対応しています。既定ではシステムの表示言語に従います。「設定 → 一般 → 表示言語」で手動で選ぶこともでき、**再起動後に反映されます**。コマンドラインの出力は既定では中国語のままで、`--lang en|ja|zh-CN` または環境変数 `DISC_IMAGE_STUDIO_LANG` で切り替えられます。翻訳の仕組みと言語の追加方法は [docs/LOCALIZATION.md](docs/LOCALIZATION.md)（英語）を参照してください。

## システムとデバイスの要件

- Windows 10 2004（20H1、19041）以降、または Windows 11。
- ビルド：.NET 10 SDK（`global.json` で 10.0.x に固定）。自分でビルドしたフレームワーク依存の GUI には .NET 10 Windows デスクトップランタイムが必要です。リリース版 EXE にはランタイムが含まれるため、別途 .NET をインストールする必要はありません。
- 即時書き込みには、Windows が認識できる CD / DVD / BD ドライブと未使用のディスクが必要です。BD-R にはシーケンシャル書き込みに対応した単層の未使用ディスクが必要です。

## ビルドと実行

```powershell
dotnet build DiscImageStudio.slnx --configuration Release
dotnet run --project src/DiscImageStudio.App --configuration Release
```

回帰チェック：

```powershell
dotnet run --project src/DvdImageSolver --configuration Release -- selftest
dotnet run --project tests/DiscImageStudio.ArchitectureTests --configuration Release
dotnet run --project tests/DiscImageStudio.LocalizationTests --configuration Release
python tools/check_locales.py
```

バージョンの規則、CI による EXE の生成、リリース草案のチェック、MSIX のパッケージ化と署名の手順は[リリースガイド](docs/RELEASING.md)（中国語）を参照してください。

## コマンドライン

引数なしで起動すると GUI が開き、引数を付けると自動化ツールとして動作します。

| コマンド | 説明 |
| --- | --- |
| `solve` | 固定の高速テクスチャマッピング（CW 方向）で DVD イメージを生成します。白黒テクスチャはディザ混合を選べます |
| `encode` | 1 つの DVD ECC ブロックを NRZI チャンネルレベルへエンコードします |
| `calibrate` | 校正投影：生成時の形状で DVD の元画像をサンプリングし、実測の形状でディスク面へ投影します |
| `simulate` | 実測の半径で、生成した DVD ISO の読み戻しをシミュレートします |
| `selftest` | DVD エンジンの決定的な回帰セルフテストを実行します |
| `cd-generate` | ディスク内ファイルがなければ WAV + CUE / RAW トラックを、`--data-dir` を指定すると CD データディスクの ISO を生成します |
| `cd-preview-warp` | 校正投影：実測の形状で CD の元画像を投影します |
| `cd-preview-track` | 校正した形状で、既存の WAV / RAW の CD-DA トラックを描画します（読み戻しシミュレーション） |
| `bd-generate` | 完全な BD-R ISO を生成し、`<iso>.json` のサイドカーを書き出します |
| `bd-info` / `bd-probe` / `bd-encode` | BD のパラメーター、生ペイロードのプローブ、1 つの ECC クラスターの診断を書き出します |

共通オプション `--lang <zh-CN|en|ja>` で出力言語を選べます。各コマンドの詳しいパラメーターは、対応する文書を参照してください。

## ソースの構成

```text
src/DiscImageStudio.Core/          メディア非依存のモジュール契約、プロジェクト/プリセットのモデル、タスクとコマンドカタログ
src/DiscImageStudio.Imaging/       CD/DVD/BD で共通の画像前処理とディザの補助
src/DiscImageStudio.Burning/       Windows IMAPI2 のデバイス列挙、ストリームバッファー、安全な書き込み層
src/DiscImageStudio.Cd/            CD-DA 生成コアと CD モジュール
src/DiscImageStudio.Dvd/           DVD エンジンのアダプターモジュール
src/DiscImageStudio.BluRay/        BD-R の形状、17PP/ECC、3T/6T マッピング、ストリーミング生成、ISO
src/DiscImageStudio.Iso/           UDF 2.01 / ISO9660 ライター
src/DiscImageStudio.Localization/  依存のない UI 文言のローカライズ（zh-CN / en / ja の翻訳表）
src/DiscImageStudio.App/           WPF GUI、統一コマンドライン、ストア用リソース
src/DvdImageSolver/                分離された DVD エンコード/求解エンジン
tests/                             アーキテクチャとローカライズの回帰テスト（サードパーティのテストフレームワークなし）
packaging/                         MSIX マニフェストとパッケージ化スクリプト
tools/                             独立した補助ツールとアセット生成スクリプト
docs/                              アーキテクチャ、エンジン、プリセット、リリースなどの文書（中国語）
```

## ドキュメント

以下の文書は、現在中国語で書かれています。

| 文書 | 内容 |
| --- | --- |
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | モジュールの抽象化、レイヤー間の境界、拡張の規則 |
| [DVD_ENGINE.md](docs/DVD_ENGINE.md) | DVD の NRZI 画像制約ソルバーの規約、エンコードの流れ、セルフテストの範囲 |
| [CD_AUDIO_COMPATIBILITY.md](docs/CD_AUDIO_COMPATIBILITY.md) | CD トラックの形式、バイトオーダー、ゴースト（二重像）の調査 |
| [ADDING_BLURAY.md](docs/ADDING_BLURAY.md) | Blu-ray モジュールの物理モデル、生成/書き込みの流れ、拡張ポイント |
| [DISC_PRESETS.md](docs/DISC_PRESETS.md) | 編集可能なディスクプリセットの階層 JSON 形式とマージ規則 |
| [CALIBRATION.md](docs/CALIBRATION.md) | 写真の補正（ディスク本体の最外縁と中心孔の縁の指定）、曲線の校正、DVD の 3 次トラックピッチ近似 |
| [PHYSICAL_MODEL_AUDIT.md](docs/PHYSICAL_MODEL_AUDIT.md) | CD / DVD 生成の物理モデルの検証 |
| [VALIDATION.md](docs/VALIDATION.md) | 各回のビルド、回帰、実ディスクテストのローカル検証記録 |
| [RELEASING.md](docs/RELEASING.md) | GitHub Release、MSIX のパッケージ化、署名テスト、ストア申請の流れ |
| [LOCALIZATION.md](docs/LOCALIZATION.md) | UI 翻訳の仕組みと、言語の追加・修正の方法（英語） |
| [CHANGELOG.md](CHANGELOG.md) | バージョンごとの変更履歴 |

## 参照規格

Disc Image Studio の実装と物理モデルは、主に次の規格を参照しています。

| メディア | 規格と版 | 本プロジェクトで採用している範囲 |
| --- | --- | --- |
| CD-R | [ECMA-394 第 1 版、2010 年 12 月](https://ecma-international.org/publications-and-standards/standards/ecma-394/) — *Recordable Compact Disc Systems CD-R Multi-Speed*（[公式 PDF](https://ecma-international.org/wp-content/uploads/ECMA-394_1st_edition_december_2010.pdf)） | CD-R メディア、マルチスピード記録、CD-DA との互換性。本プロジェクトは CD-DA トラックを生成します。 |
| DVD 共通フォーマット / DVD-ROM | [ECMA-267 第 3 版、2001 年 4 月](https://ecma-international.org/publications-and-standards/standards/ecma-267/) — *120 mm DVD – Read-only disk*（[公式 PDF](https://ecma-international.org/wp-content/uploads/ECMA-267_3rd_edition_april_2001.pdf)） | DVD のデータフレーム、ECC、EFMPlus 符号化、附属書 G の変換表。記録型ディスクのメディア規則は ECMA-359 も参照します。 |
| DVD-R | [ECMA-359 第 1 版、2004 年 12 月](https://ecma-international.org/publications-and-standards/standards/ecma-359/) — *80 mm (1,46 Gbytes per side) and 120 mm (4,70 Gbytes per side) DVD Recordable Disk (DVD-R)*（[公式 PDF](https://ecma-international.org/wp-content/uploads/ECMA-359_1st_edition_december_2004.pdf)） | 単層 DVD-R の形状、セクター、誤り訂正、符号化。規格には片面と両面のメディアが含まれ、両面は 2 層を意味しません。 |
| BD-R | [ISO/IEC 30190:2021 第 3 版、2021 年 1 月](https://www.iso.org/standard/80770.html) — 120 mm の単層（25.0 GB）および 2 層（50.0 GB）の記録型 BD の情報交換と記録の規格 | 実験モードでは、単層 25 GB のトラックとセクターの配置、誤り訂正、符号化を採用しています。2 層ディスクには現在対応していません。 |

CD の共通符号化部分は [ECMA-130 第 2 版（CD-ROM、1996）](https://ecma-international.org/publications-and-standards/standards/ecma-130/)も参照しています。

## 書き込みとメディアの安全に関する注意

- Disc Image Studio はドライバーやサービスをインストールしません。即時書き込みは Windows 標準の IMAPI2 を使い、未使用ではないメディアは拒否します。
- 即時書き込みは実ディスクでの報告がいくつかありますが、デバイス・メディア・書き込み経路ごとの検証範囲はまだ限られており、実験的な機能です。使えなくなっても構わないテスト用メディアだけを使ってください。書き込みの中断、停電、生成速度の不足により、ディスクが使えなくなる可能性があります！
- 実際に見える効果は、ディスク、ドライブ、ファームウェア、書き込み方式によって変わります。
- ハイブリッドフォルダーは、書き込みを始める前にディレクトリを走査して配置を固定します。その間に元ファイルのサイズが変わるとタスクは安全に終了しますが、すでに書き込みが始まったディスクは使えなくなる可能性があります。
- BD-R のシーケンシャル書き込みにはスペアエリアによる再割り当てがありません。中断した場合、同じディスクには通常それ以上書き込めません。

## プライバシーとセキュリティ

- Disc Image Studio はネットワークに接続せず、ファイルをアップロードせず、テレメトリーも収集しません。詳しくは[プライバシーポリシー](PRIVACY.md)（中国語）を参照してください。
- セキュリティ上の問題は、公開 Issue では公表しないでください。[セキュリティポリシー](SECURITY.md)（中国語）に従い、GitHub の非公開報告機能を使ってください。

## コントリビューション

問題の報告やコードの提供を歓迎します。開発環境の構築、モジュール境界の規則、テストの要件、Pull Request の手順は[コントリビューションガイド](CONTRIBUTING.md)（中国語）を参照してください。翻訳の修正も大歓迎です。[docs/LOCALIZATION.md](docs/LOCALIZATION.md) を参照してください。

## ライセンス

本プロジェクトは [GNU GPL v3.0 or later](LICENSE)（`GPL-3.0-or-later`）で公開されています。CD のインターリーブ実装には CDImage から移植して修正したコードが含まれ、全体として互換性のある GPL ライセンスで配布されます。バイナリや改変版を配布する場合は、対応するソースコードの提供などの要件を守ってください。サードパーティのフォントやコンポーネントはそれぞれのライセンスに従います。[ライセンスの説明](LICENSE-NOTICE.md)と[サードパーティの通知](THIRD-PARTY-NOTICES.txt)を参照してください。全文はアプリの「バージョン情報」ページでもオフラインで確認できます。

## 謝辞

CD のトラックを生成してディスク面に見える画像を作る方法を示してくれた [arduinocelentano 氏の CDImage プロジェクト](https://github.com/arduinocelentano/cdimage)に感謝します。本プロジェクトの CD インターリーブ実装は、その関連コードを移植して修正を加えたもので、CD 面への描画の発想もそこから着想を得ています。CDImage の原作者と貢献者の仕事は、本プロジェクトの重要な土台になりました。

---

## Star History

<a href="https://www.star-history.com/?repos=jiafeimiao-k-cat%2Fdiscimagestudio&type=date&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/chart?repos=jiafeimiao-k-cat/discimagestudio&type=date&theme=dark&legend=top-left" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/chart?repos=jiafeimiao-k-cat/discimagestudio&type=date&legend=top-left" />
   <img alt="Star History Chart" src="https://api.star-history.com/chart?repos=jiafeimiao-k-cat/discimagestudio&type=date&legend=top-left" />
 </picture>
</a>
