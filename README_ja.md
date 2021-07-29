<p align="center"><img src="https://avatars.githubusercontent.com/u/50210138?s=200&v=4" alt="Prefabs Logo"></p>

<p align="center"><i>Prefabsコミュニティーのサポ－トがなければ完成しませんでした！</i></p>

![Header](https://user-images.githubusercontent.com/6299186/126218956-e69d3f75-22ab-4dd8-aee3-ad1aaa4c9f97.png)

VRChatのSDK3ワールド用のビリヤード台です。 8ボール、9ボール、4ボール（日本ルール/韓国韓国ルール）を遊びませんか？ Udon Networking Updateにより、同じワールドに複数のテーブルを設置できるようになりました！

このPrefabは以下のように自由に設置することができます：

- シーン内のどの場所にも設置可能
- 回転可能
- スケール可能
- 回転台の上に設置可能
- 何度でもプレイ可能（常識的な範囲内で）
- PC/Quest両対応（ただし、QuestにはCPU負荷が高め）

改造、再利用、再配布は100%自由です。


開発者への連絡はこちら:

@FairlySadPanda on Twitter

FairlySadPanda#9528 on Discord

# 設置方法

前提：

1. 最新のVRChat SDK3がインストールされたプロジェクト
2. 最新のUdonSharp (https://github.com/MerlinVR/UdonSharp)
3. TextMeshPro

推奨：

1. [CyanEmu](https://github.com/CyanLaser/CyanEmu)（Udonエミュレーター）
2. [VRWorldToolkit](https://github.com/oneVR/VRWorldToolkit)（ワールド開発サポート）

インストール：

1. 最新リリースのunitypackageをダウンロードします。: https://github.com/FairlySadPanda/vrcbce/releases
2. VRCワールドのUnityプロジェクトでダウンロードしたunitypackageを開きます。
3. unitypackage内のアセットを全てインポートします。
4. Projectフォルダの「VRCBilliards」の中の「PoolTable」をシーンにドラッグ&ドロップします。

# Unity由来のバグ

prefab内のテキストが巨大化してしまうことがあります。このときは巨大化したTextMeshProのコンポーネントそれぞれのsizeを小さくします。（デフォルトの36に戻されてしまうため）

# 「コミュニティ」について

このプロジェクトは Harry_T's 8Ball prefabのフォークです。これはオリジナルの代替品ですが、競合するものではありません。このprefabは「Community Edition」としてコードを大幅に簡素化して変更をしやすくしています。これらはMITライセンスで提供され、prefabの変更やゲームモードの追加、バグの修正、学習への使用に対してオープンであることを約束します。

誰もがこのprefabを編集できます。

# Pull Request
このProjectは通常のUnity/C#のスタイルで書かれています。C#のスタイルは様々なものがありますが（開発チームが独自に定めたりします）
Unityドキュメント、Unity Example Scripts、[Microsoftのベストプラクティス](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions) を参考にしてください。

  
  - 変数はBehaviourの一番上に書く。
  - セキュリティのためRPCを禁止する場合を除き（Udon特有の用法）、プロパティ名やメソッド名の先頭にアンダースコア（`_`）を使うことはできるだけ避けます。。
  - プロパティと引数には camelCase を、他には PascalCase を用います。

# 原作者

このprefabの原作者はHarry_Tです。Harry_TはこのリポジトリをDCMAしようとしましたが（そして失敗しました）、自分のGitHubでパブリックドメインとして公開していることに気づいていませんでした。現在はGitHubやTwitterアカウントを削除して行方不明です。しかし、原作者として引用し、敬意を払うのは当然のことです。また、直接このプロジェクトにも少し貢献してくれました。

# UdonChips 連携

バージョン1.2.1から [UdonChips](https://lura.booth.pm/items/3060394) に対応しました！

UdonChip連携を有効にするには: 
 
  1. プロジェクトに一つの「UdonChips」オブジェクトを設置します。
  2. シーンのVRCBilliardsオブジェクトの「Enable UdonChips」オプションを有効にします。

VRCBilliardsのコアとなるPoolStateManagerスクリプトにオプションがあります。
 
  1. UCを支払ってゲームに参加します。
  2. Raisingを有効にすると、複数回支払うことができます。
  3. 自分一人のゲームでもUCを獲得できます。
  4. 支払いと報酬の金額はPoolStateManagerスクリプトで変更できます。
  5. 参加ボタンの表示メッセージはPoolMenuスクリプトで変更できます。
