# rules JSON 契約

## 作成と検証の入口

新規作成では、手書きの空オブジェクトから始めず、dotmet が出力するテンプレートを正本にする。

```bash
dotmet rules create --kind template --outputPath .dotmet/rules.json --pretty
dotmet rules validate .dotmet/rules.json --repositoryRoot . --pretty
```

利用中の dotmet が受理する正確な機械契約を確認するときは、`dotmet rules schema` を使う。
この資料は rules を設計するための意味契約を示し、JSON Schema の代わりにはならない。

## 必須のルート構造

rules のルートには、次の五つのセクションが必要である。
しきい値を設定しない場合も `thresholds` は空オブジェクトとして残す。

```json
{
  "subjects": {
    "owned": [],
    "external": []
  },
  "classifiers": {
    "codeKind": [],
    "domain": [],
    "layer": []
  },
  "policies": {
    "dependencies": []
  },
  "coverage": {
    "ownedSubjectsMustClassify": [],
    "localSubjectsMustBeOwned": []
  },
  "thresholds": {}
}
```

- `subjects.owned` は `findings` の主語にしてよい改善対象を定義する。
- `subjects.external` は依存ポリシー上で external として扱う対象を、明示的な matcher で補足する。
- `classifiers` は `codeKind`、`domain`、`layer` の各分類規則を持つ。
- `policies.dependencies` は許可する依存だけを定義する。
- `coverage.ownedSubjectsMustClassify` は owned subject に必須の分類軸を列挙する。
- `coverage.localSubjectsMustBeOwned` は owned でなければならないローカル subject の範囲を定義する。
- `thresholds` は根拠のあるメトリクスしきい値だけを持つ。

解決済みの外部参照と unowned support は、`subjects.external` に列挙しなくても `subject: "external"` に一致できる。
`subjects.external` は、既定の external 扱いに対して matcher による明示的な補足が必要な場合だけ使う。

## マッチ条件

match clause で使えるキーは次のとおりである。

- 完全一致: `projectExact`、`assemblyExact`、`tagExact`、`subjectKindExact`、`namespaceExact`、`pathExact`
- 前方一致: `namespacePrefix`、`pathPrefix`
- パス条件: `pathSegment`、`pathSuffix`

評価規則は次のとおりである。

- `matches` 配列の要素同士は OR として評価する。
- 一つの match clause にあるフィールド同士は AND として評価する。
- 一つのフィールドにある値同士は OR として評価する。
- 文字列比較は序数比較かつ大文字と小文字を区別する。
- namespace と path の prefix はセグメント境界を尊重する。
- classifier は最も具体的な一致を採用する。同じ具体度で複数の規則に一致した場合は曖昧であり、coverage failure になる。

広い一致を、未分類を隠す受け皿として追加しない。
複合条件が設計境界を表す場合は、一つの match clause に必要な条件をまとめる。

## 依存ポリシー

依存ポリシーは allow-only である。
ある依存元に複数の `from` が一致した場合、それぞれの `allow` を結合して許可範囲を決める。

- `subject` は `owned` または `external` を取る。
- `*` は、その軸で分類済みの任意のラベルを表す。
- `allow` 内の `$same` は、その軸における依存元と同じラベルを表す。
- policy selector で使える分類軸は、`coverage.ownedSubjectsMustClassify` に列挙した軸だけである。

`*`、`subject: "owned"`、`subject: "external"`、分類全体への許可は、検出したい境界越境まで許可しない場合にだけ使う。

## しきい値

しきい値は包含的な上限である。
計測値がしきい値と等しい場合は finding を生成せず、しきい値を超えた場合に review signal を生成する。

- `thresholds` で省略したメトリクスは、dotmet の組み込み標準値を継承する。
- リポジトリ固有の値は、組み込み標準値を維持するか、より小さい値で厳しくする場合だけ有効である。
- 組み込み標準値より大きい値は `RULES_THRESHOLD_WEAKENED` となり、有効な rules 構成を作れない。
- 現在の組み込み標準値は `dotmet rules create --kind standard --pretty` で確認する。標準値を別の文書から転記して判断しない。

## coverage の読み方

`rulesCoverage = full` は、必須の ownership と classifier coverage が満たされ、owned dependency edge がすべてポリシー評価された状態を表す。
これは、設計境界や許可範囲が正しいことも、違反がないことも保証しない。

`partial` または `none` の場合は、未分類、曖昧な分類、未評価の依存、ポリシー未適用、または実効的な owned scope の不足を解消する。
`full` の後に、主語、分類、許可が意図した境界を検出するかを別途レビューする。
