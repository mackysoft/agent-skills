---
name: "dotmet-analysis"
description: "dotmet の解析レポートを読み、解析の成立状態、入力のずれ、rules が適用された範囲、`findings` の分類、現在の作業範囲への帰属、次の作業種別を整理する。`analyze`、`diff`、`doctor`、`rules validate` の失敗調査、解析確認、解析結果の整理で使う。修正と再解析まで進める作業は dotmet-repair-loop。"
---

# dotmet-analysis

## 目的
dotmet の解析を実行し、レポートを読んで成立状態、信頼範囲、原因、次の作業種別を整理する。

## フロー

### Phase 1: 入力確認
解析前に、目的、入力、差分を確認する。

- 解析確認か、状態把握か、失敗調査か、結果整理か。
- 指定された解析対象、target set、rules、または標準配置の入力を使うか。
- 変更差分の確認では、比較元が決まっているか。
- 差分の対象がコード、rules、target set、実行環境のどれか。

差分確認では、差分に関係する解析対象を優先する。
状態把握では、標準配置の target set と rules を優先する。

### Phase 2: 解析実行
目的に合う dotmet の解析を実行する。
指定された入力がなければ、標準配置の target set と rules を前提にする。

### Phase 3: レポートの契約と種別
`contractVersion`、`reportKind`、`engineVersion` を読み、JSON レポートの契約と出力元を確認する。
`reportKind` が不明な場合は、後続の内容を既知のレポートとして読まない。

| レポート | 読む範囲 | 避ける読み方 |
| --- | --- | --- |
| `doctor` レポート | `diagnostics`、`diagnosticCoverage`、`partialFailures`。 | コード設計の良し悪しを判断する。 |
| `rules validate` レポート | `rulesConfig`、`validationCoverage`、`issues`、`errors`。 | 改善対象への rules 適用結果を判断する。 |
| `analyze` レポート | `target`、`comparison`、`analysisCompleteness`、`execution.rulesCoverage`、`execution.filters`、`summary`、`findings`。 | `comparison` が成立していないレポートを差分解析として扱う。 |
| `diff` レポート | `comparison`、`summary`、`entries`、`partialFailures`、`errors`。 | 全体品質の判定に使う。 |

`diff` レポートの `verdict = "pass"` は、差分レポートの構築と比較が成立した状態として読む。
全体解析の `findings` 数を、差分解析の失敗数として扱わない。

### Phase 4: 成立状態
`status`、`verdict`、`errors`、`partialFailures` を読む。
`verdict` はレポート種別ごとの意味で読み、コード全体の品質、rules の妥当性、target set の妥当性へ拡大しない。
`status = error`、`errors`、`partialFailures` がある場合は、解析対象の解決、復元、ワークスペース読み込み、`rules validate`、`comparison`、解析器、実行環境のどれに由来するかを分ける。
後続の内容があっても、原因が確定するまで `findings` を評価しない。

### Phase 5: 入力状態
`target`、`targetSet`、`rules`、`comparison`、`execution.filters` を読む。
解析対象、比較元、表示範囲が目的と合っていない場合は、`findings` ではなく入力のずれとして扱う。

### Phase 6: 信頼範囲
`completeness` と `coverage` 関連情報を読み、結果を信頼できる範囲を決める。

| 情報 | 読み方 | 注意 |
| --- | --- | --- |
| `rules validate` レポートの `validationCoverage` | rules の構成が検証で到達された範囲。 | 改善対象への rules 適用範囲とは分ける。 |
| `analyze` レポートの `execution.rulesCoverage` | 改善対象へ rules が届いた範囲。 | 設計の正しさや `findings` の有無とは分ける。 |
| `execution.rulesCoverage.localInventory` の問題 | 改善対象にするローカル `subject` の漏れや、改善対象ではないローカル `subject` の扱い。 | rules 側で判断する必要がある状態として読む。 |
| `execution.rulesCoverage.policyCoverage` の問題 | 未分類 `subject`、曖昧な分類、未評価の依存関係、未適用の依存ルール。 | rules 側で判断する必要がある状態として読む。 |
| 外部依存を使う rules | `execution.targeting.coverage` と `partialFailures` も合わせて読む。 | `externalDependencyGraph` が不完全な場合は、外部依存に関する `findings` だけで違反や rules の正しさを断定しない。 |
| `rulesCoverage` の `notConfigured` | 設計ルールが未設定の状態。 | しきい値だけの rules では単独の失敗として扱わない。 |

### Phase 7: 評価対象
`findings`、`summary`、`entries` を読み、分類に使う評価対象を確定する。
未知の `code` は `dotmet codes describe <code>` で確認する。

`findings` に含まれる `subject` が、改善対象か、依存解決や証拠に使うだけのソースか、評価から外すソースかを確認する。
改善対象ではないソースから `findings` が出ている場合は、`subjects.owned` の設定、解析対象の選択、dotmet 側の出力対象範囲のどれが原因かを分類する。
rules 入力が変わった後のレポートでは、`findings` の増減だけで作業結果を判断しない。
増減の原因をレポートだけで確定できない場合は、rules の確認が必要な状態として分類する。

### Phase 8: 実行出力の照合
標準出力、終了コード、`stderr`、`findings` 0 件、フィルター後の表示は、JSON レポートと食い違う場合に採用判断の根拠にしない。

### Phase 9: `findings` の分類
`findings` は、合意済み rules へのコード違反、rules の古さ、解析対象のずれ、既存負債、確認事項に分類する。
設計改善用の rules に対する `findings` は、コード修正対象または確認事項として分類する。

改善対象ではないソースの `findings` は、入力、解析対象、`subjects.owned` の設定、dotmet 側の出力対象範囲として分類する。
rules 入力が変わっている場合は、結果変化の確認を `dotmet-rules` の対象にするか、コード修正へ進むかを分類する。

`findings` の同じ項目を、コードの問題と rules の問題の両方に入れない。
判断が分かれる場合は、候補、根拠、決めるべき点を確認事項として残す。

### Phase 10: 診断分類
診断結果は次に分類する。

| 分類 | 意味 | 次の作業種別 |
| --- | --- | --- |
| `code-change-needed` | コードが合意済み rules に違反している。 | コード修正または修正範囲を残す。 |
| `rules-update-needed` | rules が現在の合意済み設計やコード構成に追従していない。 | `dotmet-rules` で rules の更新またはレビューへ進む。 |
| `target-update-needed` | 解析対象の選択や target set が現在のプロジェクト構成とずれている。 | `dotmet-targets` で解析対象を見直す。 |
| `configuration-or-environment` | 入力、復元、ワークスペース読み込み、参照解決、実行環境が原因で解析できていない。 | 設定、依存関係、実行環境を切り分ける。 |
| `dotmet-issue` | 入力と設定が妥当なのに dotmet 側の失敗が疑われる。 | 再現条件、入力、レポートを残す。 |
| `decision-required` | rules を変えるか、コードを直すか、設計目標を変えるかの判断が必要である。 | 候補、根拠、影響を確認事項として残す。 |
| `report-only` | 修正せず、結果の説明だけを整理する。 | 診断結果と残った論点を残す。 |

複数の問題がある場合は、主原因、従原因、影響範囲、確認が必要かを分ける。
分類は次に必要な作業種別を表すものであり、作業の実施結果として書かない。
診断では、修正へ進むかどうかを決めない。
後続の判断に使えるように、現在の作業範囲への帰属、修正対象、確認事項を残す。

### Phase 11: 作業の区切り
レポート診断は、次の状態まで進めて区切る。

| 状態 | 意味 |
| --- | --- |
| 診断完了 | レポートの種別、成立状態、信頼範囲、主原因、次の作業種別が整理されている。 |
| 診断完了、確認事項あり | 診断は成立し、設計判断、解析対象、rules、コード修正のどれに進むか確認が残っている。 |
| 未完了 | レポートを既知の契約として読めない、解析が成立していない、または診断に必要な入力が不足している。 |

### Phase 12: 診断状態を残す
診断後は、次の判断に必要な状態を残す。

1. 診断分類と確認事項の有無。
2. 主原因、従原因、信頼範囲。
3. 実行した解析、レポート種別、入力。
4. `comparison`、`execution.filters`、`coverage` 関連情報の影響。
5. `findings`、既存負債、改善対象外、確認事項。
6. 次に必要な作業種別。
7. 問題が現在の作業範囲、既存状態、入力、環境、判断事項のどれに帰属するか。
8. 修正対象が一つに絞れるか、確認事項が残るか。
