# Agent Skills ソース定義契約

## 対象

この文書は、同じ配布物に含まれる Agent Skills CLI が受け付けるソース入力の公開契約を定める。

ソース入力は、`bundle.json`、`definitions` 以下のメタデータ、本文テンプレート、参照テンプレートで構成する。

## ディレクトリ構造

ソース入力は次の固定深度で配置する。

```text
<bundle-root>/
  bundle.json
  definitions/
    <category>/
      <skill-name>/
        skill.json
        SKILL.md.template
        references/
          <reference-name>.md.template
```

- `bundle.json` と `definitions` は必須である。
- `definitions` には一つ以上のスキル定義を置く。
- `definitions` 直下の各ディレクトリを category、その直下の各ディレクトリをスキル定義として読み取る。これより深いディレクトリはスキル定義として探索しない。
- 各 category には一つ以上のスキル定義を置く。category 直下へ `skill.json` を置く形式は受け付けない。
- 各スキル定義には `skill.json` と `SKILL.md.template` が必要である。`references` は任意である。
- 読み取るパスは、それぞれの所有ディレクトリ内に解決できなければならない。`references` ディレクトリとその内容に、シンボリックリンクなどの再解析ポイントは使用できない。

## `bundle.json`

`bundle.json` は、次の3プロパティだけをこの順序で持つ正規JSONである。

```json
{
  "schemaVersion": 1,
  "catalogId": "com.example.skills",
  "skillBundleVersion": 1
}
```

| プロパティ | 型 | 制約 |
| --- | --- | --- |
| `schemaVersion` | 32 ビット整数 | `1` |
| `catalogId` | 文字列 | 1〜255 文字。ピリオド区切りの各区間は ASCII 小文字または数字で始まり、以降は ASCII 小文字、数字、ハイフンを使える。区間の末尾をハイフンにせず、空の区間を含めない。 |
| `skillBundleVersion` | 32 ビット整数 | 1 以上 |

ファイルは UTF-8 のバイト順マークなし、LF 改行、2 文字の空白によるインデント、末尾改行を含む上記形式にする。プロパティの追加、省略、並べ替え、または正規形式と異なる空白は受け付けない。

## category と skill name

category と skill name はメタデータへ書かず、ディレクトリ名から導出する。どちらも大文字と小文字を区別し、次の構文に従う。

```text
^[a-z0-9][a-z0-9-]*$
```

skill name は、category が異なる場合も含めて、同じ `definitions` 全体で一意にする。`dependencies` とスキル選択には、このディレクトリ名をそのまま使う。

## `skill.json`

`skill.json` のルートはJSONオブジェクトとし、次の4プロパティだけをこの順序で置く。

```json
{
  "schemaVersion": 1,
  "displayName": "Example Review",
  "description": "完成した例をレビューする。",
  "dependencies": []
}
```

| プロパティ | 型 | 制約 |
| --- | --- | --- |
| `schemaVersion` | 32 ビット整数 | `1` |
| `displayName` | 文字列 | 空または空白だけの値にしない。 |
| `description` | 文字列 | UTF-16 コード単位で 1〜1024。空白だけの値にしない。 |
| `dependencies` | 文字列の配列 | 依存先の skill name を置く。 |

`category`、`skillName`、参照ファイル一覧など、配置から導出する情報は追加しない。未知のプロパティ、プロパティの省略、重複、並べ替えは受け付けない。
ファイルは UTF-8 の JSON とする。JSON のコメントと末尾カンマは使用できない。プロパティ順序以外の空白、インデント、改行、バイト順マーク、末尾改行は固定しない。

## `dependencies`

`dependencies` は次をすべて満たす。

- 各値は、この契約の skill name 構文に従う。
- 自分自身、重複、同じ `definitions` に存在しない skill name を含めない。
- 依存関係全体に循環を作らない。
- 同じ `definitions` にある別のスキルを、本文または参照内でドル記号を前置した skill name として参照する場合、その名前を `dependencies` に置く。宣言した依存先も、本文または参照内で同じ形式により参照する。

本文とすべての参照は Markdown 上の役割を区別せず、コードブロック、例、説明を含む全テキストを照合する。参照として受け付けるのは、`$` の直後に skill name を置き、その末尾に ASCII の英数字、アンダースコア、ハイフンが続かない表記である。`${skill-name}` は受け付けない。

配列内の順序は入力契約で固定しない。

## `SKILL.md.template`

- YAML frontmatter を含めない。
- 先頭の空白を除いた内容を `---` またはトップレベル見出しの `# ` で始めない。

## 参照テンプレート

`references` が存在する場合は、参照テンプレートを直下の通常ファイルとして置く。隠しファイルを含め、直下には `.md.template` ファイル以外を置かない。サブディレクトリや、シンボリックリンクなどの再解析ポイントは使用できない。

ファイル名は `.md.template` で終わる安全な単一パス要素にする。末尾の `.template` を除いた名前が `.md` で終わり、空、`.`、`..`、絶対パス、スラッシュ、バックスラッシュを含む名前は使用できない。同じディレクトリに同名の参照は置けない。

同じ配布集合の skill name への表記は `dependencies` の照合対象になる。
