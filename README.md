# Discord Proxy Launcher

* Como **não** assistir/compartilhar telas no Discord.

## O que ele faz

1. Salva a configuração atual de proxy do usuário do Windows.
2. Ativa `181.39.25.196:8118`.
3. Fecha as instâncias do Discord.
4. Abre o Discord novamente.
5. Mantém o proxy ativo por 10 segundos.
6. Restaura exatamente a configuração anterior.

Se ocorrer erro após a ativação do proxy, o programa tenta restaurá-lo no bloco de finalização.
A janela também impede fechamento normal enquanto a operação está em andamento.

## Compilar

### Jeito fácil

Dê dois cliques em:

`build.bat`

O arquivo final será copiado para:

`dist\DiscordProxyLauncher.exe`

Esse é o único arquivo que você precisa.

### Visual Studio

Abra `DiscordProxyLauncher.csproj` no Visual Studio 2022 e compile em Release.

## Observação

Durante alguns segundos, o proxy é uma configuração do usuário do Windows. Portanto, outros aplicativos que respeitem o proxy do sistema também podem usá-lo nesse intervalo.
