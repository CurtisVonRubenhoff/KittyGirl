KaleidoscopeParticle (Unity package)
制作者 Oyakawa Daiki



////// このアセットについて //////
このアセットは、色々なKaleidoscope(万華鏡)エフェクトを作ることができます。
エフェクトはParticleSystemをベースにしていますが、それぞれのParticleは独自のScriptで動かしています。
KaleidoscopeParticleは3Dエフェクトなので、魔法陣・爆発・背景など色々な用途に使えます。
Kaleidoscopeの模様を作るのは簡単です。1つの部品の位置・角度・大きさを設定すると、自動で複製されてきれいな模様になります。

詳しくは "/KaleidoscopeParticle/Demo/SimpleEffect.unity" を開いてみてください。



////// 使い方 //////
1. KaleidoscopeParticleを作成する
  Demoシーンの "KaleidoscopeParticle/Demo/Simple_Effect.unity" を開いて下さい。
  (または、 "KaleidoscopeParticle/Prefab/Kaleidoscope_Simple.prefab" をシーンに置いてください。)

2. シーンを再生・"Target" を動かす
  シーンを再生してください。 4つのパーティクルが作られます。
  試しに、 "Kaleidoscope_Simple/Ring(1)/Target" を動かしてみてください。 (position, rotation, scale)
  あなたが "Target" を動かすと, 全てのパーティクルが動きます！

3. KaleidoscopeParticleの設定を変える
  まずはシーン再生を止めてください。
  "Ring(1)"オブジェクトのKaleidoscopeコンポーネントを見てください。
   - Base Object : 先ほどの "Target" のことです。
   - Circle Value : 円の上にあるパーティクルの数です。
   - X Mirror : trueのとき、パーティクルが対称的になります。
   - Color : 色を変えます。時間により変化させることもできます。

  値を、 "CircleValue=8, XMirror=true" に変え、シーンを再生してみてください。
  より複雑な模様になっているはずです！



////// その他 //////
- 円を増やしたい。
  オブジェクト"Ring(1)"をコピーしてください。

- パーティクルをループさせたくない。
  オブジェクト"Ring(1)"のParticleSystemを見てください。
  Loopingをfalseにしてください。ちなみに、DurationはParticleの再生時間です。
  KaleidoscopeコンポーネントのColorの最後の、Alpha値を0にするときれいに消えます。

- リアルタイムに模様を変化させたい。
  オブジェクト"Target" をスクリプトで変化させると、全体も変化します。
  詳しくは、シーン"KaleidoscopeParticle/Demo/Texture_Effect.unity" を参考にしてみてください。



////// 連絡先 //////
親川大樹 Oyakawa Daiki

Email : daiki.evilone@gmail.com
Twitter : https://twitter.com/daiki_all
FaceBook : https://www.facebook.com/OyakawaDaiki