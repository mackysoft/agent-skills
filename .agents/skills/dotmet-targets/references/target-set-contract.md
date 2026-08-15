# target set JSON 契約

target set の JSON は、ルートの `contractVersion`、`id`、一件以上の `targets` から成る。
子ターゲットは `id`、`adapter` と、`solutionPath` または `projectPath` のどちらか一方だけを持つ。

```json
{
  "contractVersion": "dotmet.targetSet@1",
  "id": "product",
  "targets": [
    {
      "id": "dotnet-main",
      "adapter": "dotnet",
      "solutionPath": "Product.slnx"
    },
    {
      "id": "unity-game",
      "adapter": "unity",
      "projectPath": "Product.Unity"
    }
  ]
}
```

## ルート

- `contractVersion` は `dotmet.targetSet@1` とする。
- `id` は target set 全体を識別する安定名とする。
- `targets` は空にしない。
- 契約にないプロパティを追加しない。

## 子ターゲット

- `id` は ASCII の英数字で始め、続く文字には英数字、`.`、`_`、`-` だけを使う。長さは 128 文字以下とする。
- 子ターゲットの `id` は子同士で一意にする。
- `adapter` は `dotnet`、`unity`、`godot` のいずれかとする。
- ソリューションを指定する子ターゲットは `solutionPath` を使う。
- プロジェクトファイルまたはプロジェクトルートを指定する子ターゲットは `projectPath` を使う。
- `solutionPath` と `projectPath` を同時に書かない。
- パスは空にせず、リポジトリルートからの相対パスにする。
- 絶対パス、`../`、リポジトリ外への参照を使わない。
- 同じパスを複数の子ターゲットに割り当てない。
- 契約にないプロパティを追加しない。

## 検証

標準配置の `.dotmet/targets.json` は、対象指定を省略した `doctor` または `analyze` で解決を確認できる。
別配置の場合は次のように明示する。

```bash
dotmet doctor --repositoryRoot . --targetSet path/to/targets.json --pretty
```

JSON Schema への適合だけで完了せず、各パスが存在し、宣言した adapter で解析対象として解決できることまで確認する。
