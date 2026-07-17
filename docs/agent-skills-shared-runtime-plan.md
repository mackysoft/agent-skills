# AgentSkills shared runtime 実装計画

## 目的
uCLI に残っている product 非依存の SKILL 配布基盤を `MackySoft.AgentSkills` に寄せ、uCLI と dotmet の `skills` command が同じ package manager / materializer / integrity verifier を利用できる状態にする。

この文書は実装対象を整理するための計画であり、各 product の SKILL 本文、CLI help、README、公開 JSON envelope の正本ではない。

## 責務境界
`MackySoft.AgentSkills` は SKILL package の生成、検証、materialization、install / update / uninstall / export / doctor の domain contract と中立ロジックを持つ。

product CLI は command 名、option 名、help 文言、exit code、product 固有 message、product の `CommandResult` 形式への最終変換を持つ。uCLI や dotmet の command 層で drift 判定、action count、host capability、digest 検証、force semantics を再実装しない。

## AgentSkills に実装するもの

### 1. manifest integrity contract
- `agent-skill.json` に `manifestDigest` を追加する。
- `manifestDigest` は canonical JSON から `manifestDigest` field 自身を除外して算出する。
- Builder、canonical package reader、runtime validator、doctor が `manifestDigest` を検証する。
- `displayName`、`description`、`hostArtifacts`、manifest field set の drift を `contentDigest` drift と分けて扱う。
- no-op 判定は `contentDigest` だけではなく、`manifestDigest`、対象 host artifact digest、managed file set の全一致を必要条件にする。

### 2. managed file set verifier
managed file set は、対象 host に materialize された次の file 群とする。

- `agent-skill.json`
- `SKILL.md`
- `references/**`
- `hostArtifacts[]` で宣言された host 固有 artifact

`SkillInstalledFileSetVerifier` は単純な `bool` ではなく、少なくとも次を返せる result model を持つ。

- `missingFiles[]`
- `extraFiles[]`
- `extraDirectories[]`

file-set verifier は「期待された file があるか」「期待外の file があるか」「unsafe path / symlink / unsupported entry がないか」を扱う。expected file が存在し内容だけが異なる場合の digest 判定は、既存の `SkillInstalledContentDigestVerifier`、`SkillHostMaterializationInspector`、`SkillInstalledPackageIntegrityVerifier` の責務として維持する。`SkillInstalledTargetStateAnalyzer` は、それらの検証結果を drift kind へ分類し、優先順位を決定する。

expected file が欠けている場合は file-set drift、managed file set 以外の file がある場合は file-set drift または local modification として分類する。expected file が存在し digest だけが異なる場合は、対象に応じて common content drift、manifest drift、host artifact drift、frontmatter drift に分類する。

### 3. drift classification
既存の doctor drift 分類を土台に、install / update / uninstall の state analysis でも次の drift を安定した failure code / drift kind として扱えるようにする。

- common content drift
- manifest drift
- frontmatter drift
- host artifact drift
- file-set drift
- clean outdated
- local modification
- unmanaged target
- name collision
- host conflict

OpenAI / Codex の `agents/openai.yaml` drift は host artifact drift として扱い、common content drift に混ぜない。

### 4. force semantics
`--force` の安全 semantics は AgentSkills の共通契約にする。

- path safety violation は `--force` でも解除しない。
- target root 外の file は置換または削除しない。
- unrelated directory と別 skill name directory は置換または削除しない。
- official skill name directory だけを置換または削除対象にできる。
- unmanaged official skill name directory を force replace 可能にする場合は、既存の「force でも unmanaged target は拒否する」挙動からの仕様変更として扱い、state と action を分けて明示する。
- result には `forced`、`replacedFiles[]`、`removedFiles[]` 相当を返せるようにする。

`dryRun` と `printDiff` は引き続き安全確認のために使う。実書き込み直前の precondition check は残し、計画後に target が変わった場合は失敗させる。

### 5. host descriptor capability
host adapter descriptor は、表示用 path だけではなく capability を明示する。

- host id
- project scope supported
- user scope supported
- project default target path
- user default target path
- metadata artifact required
- metadata artifact path
- reload guidance

default target path と reload guidance は current default として扱い、SKILL 本文や product の report contract に埋め込まない。

### 6. deterministic materialization / export
AgentSkills は materialized directory / zip が同じ input から byte-identical になることを保証する。

- host materialization は deterministic にする。
- `SKILL.md` body と `references/**` は host で変えない。
- host adapter が変更できるのは frontmatter、host metadata artifact、install / reload guidance に限定する。
- zip entry は ordinal sort、directory entry なし、fixed timestamp、UTF-8、LF normalized content にする。
- directory export も同じ materialized file set を使う。

### 7. neutral operation projection
uCLI の `SkillsCommandResultFactory` に残っている product 非依存の payload 投影を AgentSkills に寄せる。

AgentSkills は次のような中立 DTO を返すか、既存 result から作れる projection service を持つ。ただし product の公開 JSON envelope、success / error message、exit code への写像は持たない。

- list payload
  - `skills[]`
  - `supportedHosts[]`
- export payload
  - `host`
  - `format`
  - `outputRoot`
  - `skills[]`
  - `skillCount`
  - `reloadGuidance`
- install / update / uninstall payload
  - `host`
  - `scope`
  - `targetRoot`
  - `dryRun`
  - `force`
  - `printDiff`
  - `reloadGuidance`
  - `actions[]`
  - action counts
  - `replacedFiles[]`
  - `removedFiles[]`
- doctor payload
  - `host`
  - `targetRoot`
  - `isHealthy`
  - `diagnostics[]`

action、blocked reason、diff change kind、severity は stable literal を持つ。

### 8. option parsing helpers
AgentSkills は product CLI が再利用できる domain literal parser / normalizer を提供する。

- host literal を canonical host key へ解決する。
- `project` / `user` を `SkillScopeKind` へ解決する。
- `directory` / `zip` を `SkillExportFormat` へ解決する。
- project scope と user scope の repository root / target root 使用規則を検証する。

ただし、`--host`、`--scope`、`--repoRoot` などの option 名、必須扱いの UX、repoRoot をどう受け取るか、error message の最終文言は product CLI が決める。

### 9. failure classification
AgentSkills の failure code は product CLI が exit code と error envelope へ写像しやすい分類を持つ。

- invalid input
- unsupported host
- unsafe path
- user target unavailable
- manifest invalid
- source invalid
- drift / local modification
- unmanaged target
- name collision
- host conflict
- write failure
- internal error

product CLI は分類を使って自分の `CommandResult`、exit code、error schema へ変換する。分類ロジックを product ごとに重複させない。

## uCLI / dotmet に残すもの
- CLI entrypoint 名、subcommand 名、help 文言。
- product 固有の stdout / stderr / exit code 契約。
- product の `CommandResult` envelope への変換。
- product 名入り message。
- `repoRoot` をどう受け取るかという CLI UX。
- README / docs に `skills` command を露出するタイミング。
- 公式 SKILL の文言、source definition、generated output。
- product 固有の forbidden terms / required phrases / documentation policy の CI。

## 実装順
1. `manifestDigest` と manifest drift code を追加する。
2. managed file set verifier を missing / extra / safety の構造化 result に拡張する。
3. 既存の doctor drift 分類を活かし、install / update / uninstall の state analysis に manifest drift と file-set drift 詳細を組み込む。
4. force 実行結果に replaced / removed files を追加する。
5. host descriptor capability を追加し、list payload が descriptor から作れるようにする。
6. operation result projection service と stable literal codec を追加する。
7. option parsing helper と failure classification を追加する。
8. uCLI 側の `SkillsCommandResultFactory` / `SkillsCommandOptionNormalizer` / `SkillFailureApplicationFailureMapper` から product 非依存処理を削る。
9. dotmet は AgentSkills の中立 API に薄く接続する。

## 完了条件
- uCLI と dotmet が同じ AgentSkills API で list / export / install / update / uninstall / doctor の中立結果を得られる。
- product CLI 側に digest、drift、managed file set、force safety、host capability、action count の判断が残っていない。
- product CLI 側には公開 JSON envelope、message、exit code、option UX の判断だけが残っている。
- `manifestDigest`、host artifact digest、managed file set drift、force safety を `bash scripts/verify.sh` で検証できる。
- zip export が同一 input から byte-identical output を返す。
- path safety violation は `--force` でも成功しない。
