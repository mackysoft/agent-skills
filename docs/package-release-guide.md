# パッケージリリースガイド

## 目的
この文書は、Agent Skills の NuGet パッケージ version を更新し、GitHub Release と NuGet.org へ公開する手順を固定するためのものです。

利用側 repository で公開済み package を取り込む手順ではなく、この repository から新しい package version をリリースする作業を対象にします。

## 前提
- `master` が repository の default branch である。
- `Directory.Build.props` の `<Version>` が package version の正本である。
- release tag は `<Version>` と同じ SemVer 文字列にする。先頭に `v` を付けない。
- `nuget-package` workflow は tag push で起動する。
- 次の NuGet package は同じ version で公開する。

  - `MackySoft.AgentSkills`
  - `MackySoft.AgentSkills.Cli`
  - `MackySoft.AgentSkills.Hosting`
  - `MackySoft.AgentSkills.ConsoleAppFramework`

## Version を決める
1. 現在の最新 release tag と NuGet.org の公開済み version を確認する。

   ```bash
   git tag --list --sort=v:refname
   curl -fsSL https://api.nuget.org/v3-flatcontainer/mackysoft.agentskills/index.json
   curl -fsSL https://api.nuget.org/v3-flatcontainer/mackysoft.agentskills.cli/index.json
   curl -fsSL https://api.nuget.org/v3-flatcontainer/mackysoft.agentskills.hosting/index.json
   curl -fsSL https://api.nuget.org/v3-flatcontainer/mackysoft.agentskills.consoleappframework/index.json
   ```

2. 直近 release tag から `master` までの差分を確認する。

   ```bash
   git log --oneline <PREVIOUS_VERSION>..origin/master
   git diff --stat <PREVIOUS_VERSION>..origin/master
   ```

3. SemVer の次 version を決める。

   pre-1.0 の間でも、公開 API の追加や契約変更を含む場合は minor version を上げます。patch version は、互換性のある修正だけに使います。

## Release 準備 PR
1. `origin/master` から release 準備 branch を作成する。

   ```bash
   git switch -c release/<VERSION> origin/master
   ```

2. `Directory.Build.props` の `<Version>` を更新する。

3. README の package 使用例を同じ version に更新する。

4. Release notes を作成する。

   GitHub Release に設定する notes は、一般的な changelog 形式で `Added`、`Changed`、`Fixed`、`Removed` に分けます。利用者が移行判断に使うため、公開 API、manifest contract、CLI 動作、検証契約、互換性注意を優先して書きます。

5. release 準備 branch で検証する。

   ```bash
   bash scripts/code-quality.sh verify
   bash scripts/verify.sh --configuration Release
   ```

6. release 準備 commit を作成する。

   ```bash
   git add Directory.Build.props README.md
   git commit -m "chore(release): prepare <VERSION>"
   ```

7. commit 後の SHA を使って package smoke test を実行する。

   ```bash
   bash scripts/verify-packages.sh \
     --configuration Release \
     --version <VERSION> \
     --repository-commit <RELEASE_PREPARE_COMMIT_SHA>
   ```

8. PR を作成し、CI が通過したら `master` へ merge する。

## Tag と公開
1. merge 後の `origin/master` を取得する。

   ```bash
   git fetch origin master --tags
   git rev-parse origin/master
   ```

2. `origin/master` の merge commit に release tag を作成して push する。

   ```bash
   git tag <VERSION> origin/master
   git push origin refs/tags/<VERSION>
   ```

3. `nuget-package` workflow の完了を待つ。

   ```bash
   gh run list --workflow nuget-package --limit 5
   gh run watch <RUN_ID> --exit-status --interval 10
   ```

workflow は次を実行します。

- `dotnet-verify.yaml` による 3 OS 検証
- tag と default branch の source guard
- package 作成、成果物個数の確認、smoke test
- NuGet.org への trusted publishing
- NuGet.org で全 package が取得可能になるまでの待機
- 公開済み package の repository commit 検証
- GitHub Release への `.nupkg` mirror

## 公開後確認
1. NuGet.org で全 package の version を確認する。

   ```bash
   curl -fsSL https://api.nuget.org/v3-flatcontainer/mackysoft.agentskills/index.json
   curl -fsSL https://api.nuget.org/v3-flatcontainer/mackysoft.agentskills.cli/index.json
   curl -fsSL https://api.nuget.org/v3-flatcontainer/mackysoft.agentskills.hosting/index.json
   curl -fsSL https://api.nuget.org/v3-flatcontainer/mackysoft.agentskills.consoleappframework/index.json
   ```

2. 公開済み `.nupkg` を取得し、repository commit が release tag の commit と一致することを確認する。

   ```bash
   bash scripts/validate-nuget-package-repository-commit.sh \
     --package-id MackySoft.AgentSkills \
     --package-path <DOWNLOADED_LIBRARY_NUPKG> \
     --expected-commit <RELEASE_TAG_COMMIT_SHA>

   bash scripts/validate-nuget-package-repository-commit.sh \
     --package-id MackySoft.AgentSkills.Cli \
     --package-path <DOWNLOADED_CLI_NUPKG> \
     --expected-commit <RELEASE_TAG_COMMIT_SHA>

   bash scripts/validate-nuget-package-repository-commit.sh \
     --package-id MackySoft.AgentSkills.Hosting \
     --package-path <DOWNLOADED_HOSTING_NUPKG> \
     --expected-commit <RELEASE_TAG_COMMIT_SHA>

   bash scripts/validate-nuget-package-repository-commit.sh \
     --package-id MackySoft.AgentSkills.ConsoleAppFramework \
     --package-path <DOWNLOADED_CONSOLEAPPFRAMEWORK_NUPKG> \
     --expected-commit <RELEASE_TAG_COMMIT_SHA>
   ```

3. GitHub Release に全 package artifact が mirror されていることを確認する。

   ```bash
   gh release view <VERSION> --json url,assets,body
   ```

4. workflow は release notes を空で作成するため、公開後に作成済み notes を設定する。

   ```bash
   gh release edit <VERSION> --notes-file <NOTES_FILE>
   ```

## 停止条件
- 4 package の一部だけが NuGet.org に存在する。
- release tag が default branch 以外の commit を指している。
- `Directory.Build.props` の `<Version>` と release tag が一致しない。
- 公開済み package の repository commit が release tag commit と一致しない。
- GitHub Release の `.nupkg` artifact が 4 個ではない。
