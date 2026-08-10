# パッケージリリースガイド

## 目的
この文書は、Agent Distribution の NuGet パッケージ version を更新し、GitHub Release と NuGet.org へ公開する手順を固定するためのものです。

利用側 repository で公開済み package を取り込む手順ではなく、この repository から新しい package version をリリースする作業を対象にします。

## 前提
- `master` が repository の default branch である。
- `Directory.Build.props` の `<Version>` が package version の正本である。
- release tag は `<Version>` と同じ SemVer 文字列にする。先頭に `v` を付けない。
- `nuget-package` workflow は tag push で起動する。
- NuGet.org の trusted publishing policy が、GitHub repository `mackysoft/agent-distribution` と workflow file `nuget-package.yaml` を許可している。
- 次の NuGet package は同じ version で公開する。

  - `MackySoft.AgentDistribution`
  - `MackySoft.AgentDistribution.Cli`
  - `MackySoft.AgentDistribution.Hosting`
  - `MackySoft.AgentDistribution.ConsoleAppFramework`

## Version を決める
1. 現在の最新 release tag と NuGet.org の公開済み version を確認する。

   ```bash
   git tag --list --sort=v:refname
   curl -fsSL https://api.nuget.org/v3-flatcontainer/mackysoft.agentdistribution/index.json
   curl -fsSL https://api.nuget.org/v3-flatcontainer/mackysoft.agentdistribution.cli/index.json
   curl -fsSL https://api.nuget.org/v3-flatcontainer/mackysoft.agentdistribution.hosting/index.json
   curl -fsSL https://api.nuget.org/v3-flatcontainer/mackysoft.agentdistribution.consoleappframework/index.json
   ```

2. 直近 release tag から `master` までの差分を確認する。

   ```bash
   git log --oneline <PREVIOUS_VERSION>..origin/master
   git diff --stat <PREVIOUS_VERSION>..origin/master
   ```

3. SemVer の次 version を決める。

   公開 API や契約の互換性が失われる変更では major version、互換性を保つ機能追加では minor version、互換性を保つ修正では patch version を上げます。

## 3.0.0 の package identity 移行
`3.0.0` は repository、namespace、assembly、CLI command、状態保存先、NuGet package ID を Agent Distribution へ統一する改名 release です。NuGet.org では公開済み package ID を改名できないため、次の対応関係で新しい package を公開します。

| 2.x まで | 3.0.0 以降 |
| --- | --- |
| `MackySoft.AgentSkills` | `MackySoft.AgentDistribution` |
| `MackySoft.AgentSkills.Cli` | `MackySoft.AgentDistribution.Cli` |
| `MackySoft.AgentSkills.Hosting` | `MackySoft.AgentDistribution.Hosting` |
| `MackySoft.AgentSkills.ConsoleAppFramework` | `MackySoft.AgentDistribution.ConsoleAppFramework` |

新しい trusted publishing policy を作成してからタグをプッシュします。新しい4つのパッケージの公開を確認するまでは、旧リポジトリを対象にした policy を削除しません。

新しい4つのパッケージの公開後、旧パッケージの全バージョンをNuGet.orgで非推奨にし、対応する新パッケージを代替パッケージとして設定します。旧パッケージは自動的に非掲載にせず、既存利用者が正確なバージョンを復元できる状態を保ちます。新パッケージには、旧名前空間、旧アセンブリ名、旧コマンド名、旧状態保存先を維持する互換エイリアスを追加しません。

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

7. release 準備 branch を push し、`skills-sync` workflow の完了を待つ。

   ```bash
   git push -u origin release/<VERSION>
   gh run list --workflow skills-sync --branch release/<VERSION> --limit 5
   gh run watch <RUN_ID> --exit-status --interval 10
   git pull --ff-only
   ```

   `skills-sync` workflow は、`release/` で始まるbranchについて、default branchのバンドル版から次の正確な版を解決します。release Actionが `bundle.json` と生成物を同じ版へ更新し、`github-actions[bot]` のrelease commitを現在のbranchへpushします。通常branchの同期はバンドル版を変更しません。

   workflowを再実行しても、default branchから求める目標版は変わりません。すでにrelease commitが存在する場合は同じ版へ収束し、さらに版を進めません。ローカルでは `bundleVersion` を変更しません。

8. bot commit 後の SHA を使って package smoke test を実行する。

   ```bash
   bash scripts/verify-packages.sh \
     --configuration Release \
     --version <VERSION> \
     --repository-commit <RELEASE_BUNDLE_COMMIT_SHA>
   ```

9. PR を作成し、CI が通過したら `master` へ merge する。

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
   curl -fsSL https://api.nuget.org/v3-flatcontainer/mackysoft.agentdistribution/index.json
   curl -fsSL https://api.nuget.org/v3-flatcontainer/mackysoft.agentdistribution.cli/index.json
   curl -fsSL https://api.nuget.org/v3-flatcontainer/mackysoft.agentdistribution.hosting/index.json
   curl -fsSL https://api.nuget.org/v3-flatcontainer/mackysoft.agentdistribution.consoleappframework/index.json
   ```

2. 公開済み `.nupkg` を取得し、repository commit が release tag の commit と一致することを確認する。

   ```bash
   bash scripts/validate-nuget-package-repository-commit.sh \
     --package-id MackySoft.AgentDistribution \
     --package-path <DOWNLOADED_LIBRARY_NUPKG> \
     --expected-commit <RELEASE_TAG_COMMIT_SHA>

   bash scripts/validate-nuget-package-repository-commit.sh \
     --package-id MackySoft.AgentDistribution.Cli \
     --package-path <DOWNLOADED_CLI_NUPKG> \
     --expected-commit <RELEASE_TAG_COMMIT_SHA>

   bash scripts/validate-nuget-package-repository-commit.sh \
     --package-id MackySoft.AgentDistribution.Hosting \
     --package-path <DOWNLOADED_HOSTING_NUPKG> \
     --expected-commit <RELEASE_TAG_COMMIT_SHA>

   bash scripts/validate-nuget-package-repository-commit.sh \
     --package-id MackySoft.AgentDistribution.ConsoleAppFramework \
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

5. `3.0.0` では、旧4パッケージを非推奨にして対応する新パッケージを案内した後、旧リポジトリ用の trusted publishing policy を削除する。

## 停止条件
- 4 package の一部だけが NuGet.org に存在する。
- release tag が default branch 以外の commit を指している。
- `Directory.Build.props` の `<Version>` と release tag が一致しない。
- 公開済み package の repository commit が release tag commit と一致しない。
- GitHub Release の `.nupkg` artifact が 4 個ではない。
