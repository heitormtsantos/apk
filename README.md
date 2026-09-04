# Raven Drop

Projeto completo do prototipo Battle Royale mobile feito em Unity 6 com URP.

## Abrir em outra maquina

1. Instale Git LFS e execute `git lfs install`.
2. Clone este repositorio normalmente.
3. Abra a pasta clonada pelo Unity Hub usando Unity `6000.5.9f1`.
4. Instale o modulo Android Build Support para gerar builds Android.
5. Abra `Assets/_Game/Scenes/BattleRoyalePrototype.unity`.

As pastas `Library`, `Temp`, `Logs` e outros caches nao sao versionadas. O Unity ira recria-las no primeiro carregamento.

## APK

O APK ARM64 para Android 8.0 ou superior esta na raiz como `RavenDrop-Android.apk`.

SHA-256:

```text
425AC7E50970B363D99FC8A0FC3A699675A86EC4F25FBA22BF5FAFD8E16CE6DE
```

## Validacao

- Unity Edit Mode: 393 de 393 testes passaram.
- Loot de armas e mochilas foi reduzido.
- Mochila equipada foi reposicionada e orientada nas costas.
- Aviao de implantacao e Airdrop usam o novo modelo texturizado.

O projeto ainda e um prototipo offline. Teste o APK em aparelhos Android fisicos antes de distribuicao publica.
