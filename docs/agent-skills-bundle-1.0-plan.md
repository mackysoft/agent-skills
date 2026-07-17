# Agent Skills 1.0 バンドル運用設計

## 目的

この文書は、Agent Skills 1.0 の定義配置、生成物、バージョン調停、GitHub Action の責務を定める。Agent Skills の実装者と、uCLI・dotmet へ導入する保守担当者が、同じ生成契約をローカルと継続的インテグレーションで利用するための運用設計として使う。

## バンドル構造

利用側リポジトリは、一つのルートに定義と生成物を配置する。

```text
skills/
  bundle.json
  definitions/
    <category>/
      <skill>/
        skill.json
        SKILL.md.template
        references/
  generated/
    bundle.json
    <skill>/
      agent-skill.json
      SKILL.md
      references/
      agents/
```

`definitions` の探索深度は `<category>/<skill>` に固定する。カテゴリーとスキル名はそれぞれのディレクトリ名から決まり、コードや `skill.json` に同じ情報を重複して定義しない。参照名は `<skill>/references/*.md.template` から決まり、`skill.json` に一覧を重複して定義しない。`generated` はカテゴリー別のサブディレクトリを持たず、各 `agent-skill.json` がカテゴリーとスキル名を保持する。

source の `bundle.json` は `schemaVersion`、`catalogId`、`skillBundleVersion` を所有する。個別の `skill.json` は、スキル固有の定義だけを所有する。generated の `bundle.json` は source と同じ識別情報に加え、生成済みパッケージ集合の `bundleDigest` を保持する。

generated の `agent-skill.json` は、インストール後も source ディレクトリに依存せず、一つのパッケージとして同一性、選択、依存解決、整合性検証を行うための manifest である。そのため `schemaVersion`、`skillBundleVersion`、`catalogId`、`category`、`skillName`、表示情報、`dependencies`、content・manifest digest、`hostArtifacts` を保持する。source から導出できる項目であっても、flat な generated 配置とインストール先では導出元が存在しないため、省略しない。

1.0 で出力する source definition、manifest、bundle の `schemaVersion` はそれぞれ `1` とする。各スキーマの現在値は、そのモデルを実装するコードを正本とする。NuGet パッケージのバージョンは `Directory.Build.props` を正本とし、スキーマバージョンや利用側の `skillBundleVersion` と連動させない。

## ダイジェスト契約

SHA-256 は小文字の16進数64文字で表し、`sha256:` などのプレフィックスを付けない。未検証の文字列は `Sha256Digest.Parse` または `TryParse` を通過した時点でダイジェスト値になる。

`bundleDigest` は生成内容の変更判定に使う。`skillBundleVersion` と、その値から派生する manifest 情報は比較対象から除外し、同じ内容ならバージョンだけが異なっても同じ値になるようにする。これにより、バージョン加算の要否をバージョン自身に依存せず判定できる。

## バージョン調停

CLI は、source のバージョン `Vs`、現在の generated のバージョンとダイジェスト `Vg`・`Dg`、定義から算出したバージョン非依存ダイジェスト `Dc` を使って次の処理を決める。

| 状態 | 結果 |
| --- | --- |
| generated が存在しない | `Vs` で初回生成する |
| `Dc == Dg` かつ `Vs == Vg` | ファイルを変更しない |
| `Dc != Dg` かつ `Vs == Vg` | source を `Vg + 1` へ更新して生成する |
| `Dc != Dg` かつ `Vs == Vg + 1` | ローカルで更新済みのバージョンを維持して生成する |
| `Dc == Dg` かつ `Vs != Vg` | 内容を伴わないバージョン差として失敗する |
| 上記以外のバージョン差 | 巻き戻し、飛び越し、二重加算の可能性があるため失敗する |

通常の build と検証専用モードは同じ調停処理を使う。`build --check` は変更が必要な場合に失敗し、source と generated を変更しない。通常の build は、source `bundle.json` と generated 全体を一つの公開単位として置き換える。途中で失敗した場合は、どちらか一方だけを更新した状態を残さない。

## CLI と Action の責務

CLI は次を所有する。

- ディレクトリ構造の解決
- 定義と generated の読み取り
- ダイジェスト計算
- バージョン調停
- source と generated の一体的な更新
- `--check` の成否

自己ホストする Agent Skills CLI は依存性の最上位にある composition root であり、Core、Hosting、ConsoleAppFramework adapter を利用する。ConsoleAppFramework 5.7.13 は command 型と dispatch を利用側アセンブリで生成し、別プロジェクトの command 型登録を CAF014 で拒否する。そのため adapter は source integration を所有し、CLI も同じ command source をコンパイルする。CLI 固有の command wrapper は作らず、adapter や Hosting から CLI への逆参照も作らない。

GitHub Action は CLI を起動する。Action にダイジェスト形式やバージョン分岐を再実装しない。

`actions/verify/action.yaml` と `actions/sync/action.yaml` を別の公開契約として提供する。副作用が異なるため、単一 Action の mode 入力にはしない。両方の入力は `root` のみ、既定値は `skills` とする。`root` は checkout 済み Git worktree 内に存在する、GitHub workspace からの相対パスに限る。`definitions` と `generated` は root 配下の固定構造なので、個別入力にはしない。利用する Agent Skills CLI のバージョンは、利用側リポジトリの .NET tool manifest が固定する。

`verify` は `build --check` だけを実行し、ファイルを変更しない。Pull Request と読み取り専用 CI は `verify` を使う。

`sync` は `build --check` で調停要否を確認し、変更が必要な場合だけ通常の build を実行する。既に stage された変更がある場合は、bot commit へ無関係な変更を取り込まないよう生成前に失敗する。その後、`<root>/bundle.json` と `<root>/generated` だけを stage し、`github-actions[bot]` の commit を作成して現在の branch へ push する。差分がない場合は commit しない。出力 `changed` は push まで成功した場合だけ `true` とする。利用側 workflow は `contents: write` を明示し、bot の直接 push を許可する branch だけで `sync` を使う。

## 利用側の移行

uCLI と dotmet は、定義を `skills/definitions/<category>/<skill>` へ移し、バンドル共通情報と旧 tier を個別 `skill.json` から削除する。生成物の同梱先は `skills/generated` のままとする。

uCLI の生成・検証スクリプトは CLI の build と `build --check` へ縮退する。dotmet のテストに埋め込まれた生成・比較処理も CLI へ移し、テストには dotmet 固有の配布ポリシーだけを残す。両リポジトリの変更検出は `skills/**` を必ず検証対象に含める。

## 導入状況

このリポジトリでは、固定深度のカテゴリー探索、source bundle、flat generated bundle、raw SHA-256、CLI のバージョン調停、`build --check`、source と generated の更新・復元境界、`verify` と `sync` の composite action を実装している。uCLI と dotmet への移行は、それぞれの利用側リポジトリで行う。

reusable workflow は、複数の利用側で同じイベント、権限、commit 方針が実運用として収束した後に必要性を判断する。
