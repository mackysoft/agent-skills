# AGENTS.md

## 原則
- ユーザーに対しては **日本語** で応答する
- ユーザーに対して応答する際は、一般的ではない略語や語彙の圧縮を避け、明瞭で具体性のある表現を用いる
- 成果物に含める各記述は「成果物の目的・読者・利用シーン」に対して必要性を説明できるものに限り、説明できない要素は出力しない。

## プロジェクト構成
- 共通ライブラリ: `src/MackySoft.AgentSkills/`
- Builder: `src/MackySoft.AgentSkills.Builder/`
- テスト: `tests/MackySoft.AgentSkills.Tests/`

## コーディング規約
- SOLID原則を遵守し、高凝集・低結合を前提にモジュール分割する
- DRY原則を遵守し、本質的な重複を避ける
- 関心事を分離し、単一責任の原則を守る
- 例外処理は契約として設計し、入力検証は早期に弾く。例外を制御フローに使わない
- 原則として例外を握りつぶさない
- レイヤー境界を明確にし、ユニットテスト可能な設計を優先する
- 背景が重要な処理には、`NOTE`コメントで意図を明示して残す
- 処理ブロックで波括弧（`{}`）を省略しない
- 命名規則は既存コードに合わせる
- 1ファイルにトップレベル型は1つまで
- コードの複雑性を上げる単純なエイリアスは用いない

## 非同期規約
- 非同期関数は必ず`CancellationToken`を伝搬させる
- `CancellationToken`を渡す場合は、原則として引数の末尾に置く
- 非同期関数は、適切な箇所で`ThrowIfCancellationRequested`を呼び出す
- 非同期関数は、原則として同期的な待機を行わない

## テスト実行
```bash
bash scripts/verify.sh
```

## コードフォーマット
```bash
bash scripts/code-quality.sh format
bash scripts/code-quality.sh verify
```
