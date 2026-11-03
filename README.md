# TrophyPop

Pop-ups de troféu no estilo PS5 para as conquistas da Steam, no Windows.

![Pop-up de troféu de ouro](docs/popup-Gold.png)
![Pop-up de platina com brilho](docs/brilho-Platinum.png)

## Como usar

1. Baixe o `TrophyPop.exe` na página de **Releases** deste repositório. Não precisa instalar nada.
2. Abra o arquivo. Um troféu aparece perto do relógio do Windows e a janela de configurações abre.
3. Crie sua chave da API em https://steamcommunity.com/dev/apikey (no domínio pode colocar `localhost`) e cole a chave no app.
4. Na Steam: Perfil › Editar perfil › Privacidade › **Detalhes de jogos: Público**.
5. Clique em **Testar conexão** e depois em **Salvar**.

Depois disso é só jogar. O app vê sozinho qual jogo da Steam está aberto.

## Tipos de troféu

A Steam não tem tipos de troféu. Aqui o tipo vem da raridade (você pode mudar os limites):

| Troféu  | Quando                                       |
|---------|----------------------------------------------|
| Ouro    | menos de 7% dos jogadores têm a conquista    |
| Prata   | menos de 25%                                 |
| Bronze  | o resto                                      |
| Platina | quando você completa 100% das conquistas     |

## Menu do ícone perto do relógio

- **Lista de troféus**: seus jogos e troféus, com filtros e ordenação. Também abre com dois cliques no ícone.
- **Configurações**
- **Testar pop-up**: mostra um exemplo de cada tipo.

![Lista de troféus](docs/lista.png)

## Feito para não pesar no jogo

- Parado, o app fica dormindo: só acorda quando a Steam abre ou fecha um jogo (o Windows avisa) e, com um jogo aberto, para uma consulta pequena à Steam a cada 5 segundos.
- Roda com prioridade abaixo do normal e no "modo de eficiência" do Windows. Em processadores com núcleos de eficiência, ele fica nesses núcleos e deixa os rápidos para o jogo.
- O pop-up é desenhado pelo processador, num cartão pequeno e a no máximo 60 quadros por segundo, para não disputar a placa de vídeo com o jogo. A janela só existe enquanto o pop-up aparece.
- Depois de cada pop-up ou janela fechada, o app devolve a memória sobrando ao Windows.
- Sons e código já vêm prontos desde a abertura do app, então nada é preparado na hora do troféu.

Medido parado: cerca de 9 MB de memória e quase nada de processador.

## Dicas

- Jogue em **tela cheia sem bordas** (ou janela). Em tela cheia exclusiva, o Windows não deixa nada aparecer por cima do jogo.
- Para usar seu próprio som, coloque `trofeu.wav` e `platina.wav` em `%APPDATA%\TrophyPop`.
- O pop-up aparece alguns segundos depois de você ganhar a conquista, que é o tempo da Steam atualizar.
- Se algo der errado, veja `%APPDATA%\TrophyPop\erros.log`.

## Compilar

Precisa do [.NET 10 SDK](https://dotnet.microsoft.com/download). O código fica na pasta `TrophyPop/` (C# / WPF).

```
dotnet publish TrophyPop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o TrophyPop-pronto
```

O `.exe` sai em `TrophyPop-pronto/`.

---

Projeto de fã, sem ligação com a Sony ou a Valve. A taça e os sons são originais deste projeto.
