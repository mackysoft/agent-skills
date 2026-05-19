# agent-skills 分離計画

## Summary
uCLI 内の `src/Ucli.Skills` と `tools/Ucli.SkillGenerator` から、SKILL 生成・配布・導入・診断の共通基盤を `/Users/makihiro/Repositories/agent-skills` へ移植する。uCLI 側には SKILL のロジックを残さず、uCLI 固有の定義・生成済み成果物・CLI 接続コードだけを残す。

## agent-skills 側
- 新規リポジトリ `/Users/makihiro/Repositories/agent-skills` を作成し、uCLI から `LICENSE`、`AGENTS.md`、`.editorconfig`、`.gitattributes`、`.gitignore`、`Directory.Build.props`、基本スクリプトを移植する。
- 構成は次にする。

```text
src/MackySoft.AgentSkills/
tests/MackySoft.AgentSkills.Tests/
tools/MackySoft.AgentSkills.Builder/
scripts/
AgentSkills.slnx
```

- `MackySoft.AgentSkills` は manifest、digest、source definition reader、canonical package reader/writer、host adapter、materialization、install/update/uninstall/export/doctor を持つ。
- `MackySoft.AgentSkills.Builder` は `agent-skills build --definitionsRoot <path> --generatedRoot <path>` を提供し、各プロダクトの `skills/definitions` から `skills/generated` を生成する。
- uCLI 固有名を排除し、namespace は `MackySoft.AgentSkills.*`、canonical manifest 名は `agent-skill.json` にする。
- 初期段階では NuGet 配布を前提にしない。uCLI からは `ProjectReference` で参照する。

## uCLI 側
- `src/Ucli.Skills` のロジックと `tools/Ucli.SkillGenerator` は削除し、共通処理は `external/agent-skills` の `MackySoft.AgentSkills` を参照する。
- uCLI 固有の SKILL 入力と生成物は次へ移す。

```text
skills/
  definitions/
  generated/
```

- `src/Ucli/Ucli.csproj` は共通ライブラリを参照し、同梱対象を `skills/generated/**/*` に変更する。

```xml
<ProjectReference Include="../../external/agent-skills/src/MackySoft.AgentSkills/MackySoft.AgentSkills.csproj" />
```

- `external/agent-skills` は `agent-skills` リポジトリの submodule とする。CI と開発環境では `git submodule update --init --recursive` で同じ参照パスを再現する。
- `src/Ucli/Hosting/Cli/Skills/` は残す。ただし責務は CLI entrypoint、option 正規化、uCLI の `CommandResult` JSON への変換、`MackySoft.AgentSkills` 呼び出しだけに絞る。

## Implementation Steps
- `agent-skills` に uCLI の規則ファイル、MIT ライセンス、基本スクリプト、solution、library、builder、tests を作成する。
- `src/Ucli.Skills` のコードを `MackySoft.AgentSkills` へ移植し、`Ucli` 名、`Official` の曖昧な命名、`ucli-skill.json` を汎用名へ置き換える。
- `tests/Ucli.Skills.Tests` を `MackySoft.AgentSkills.Tests` へ移植し、fixture path と namespace を更新する。
- uCLI 側で `skills/definitions` と `skills/generated` へ配置を変更し、`scripts/generate-skills.sh`、`scripts/verify-skills.sh`、package smoke test、docs を更新する。
- uCLI の architecture/package boundary tests から `src/Ucli.Skills` 前提を除去し、`external/` を検査対象外にする。

## Test Plan
- `agent-skills`: `bash scripts/verify.sh`
- `agent-skills`: Builder で fixture の `skills/definitions` から `skills/generated` が決定論的に生成されることを検証する。
- `agent-skills`: install/export/doctor/materialization/digest/path safety の既存テストを移植して通す。
- uCLI: `bash scripts/verify-skills.sh`
- uCLI: `bash scripts/verify.sh`
- uCLI: CLI package smoke test で `skills/generated/**` が同梱され、`ucli skills list/export/install/doctor --host openai` が動作することを確認する。

## Assumptions
- 汎用リポジトリ名は `agent-skills`、主要 namespace は `MackySoft.AgentSkills` とする。
- 初期導入では NuGet 配布を行わず、submodule + `ProjectReference` で利用する。
- uCLI / dotmet の SKILL 本文と metadata は各プロダクト側の `skills/definitions` に置く。
- `skills/generated` は生成済み配布物であり、手編集しない。
