# Discord Proxy Launcher

Aplicativo Windows Forms para reiniciar o Discord temporariamente por um proxy e restaurar, ao final, a configuração original do Windows.

## Recursos

- Seleção do proxy preferido pela interface.
- Validação real por meio da API do Discord antes de alterar o Windows.
- Fallback automático: se o selecionado falhar, os demais são testados em sequência.
- Busca de proxies públicos recentes dos Estados Unidos.
- Restauração do proxy original após sucesso ou erro.
- Bloqueio do fechamento da janela durante operações críticas.

## Proxies padrão

Os seguintes servidores dos Estados Unidos estão sempre disponíveis no seletor:

```text
181.39.25.196:8118
159.112.235.87:80
172.67.167.93:80
172.64.149.154:80
172.67.181.184:80
```

## Buscar mais servidores

O botão **Buscar mais servidores proxy** consulta via `GET`:

```text
https://proxyfreeonly.com/api/free-proxy-list?limit=10&page=1&sortBy=lastChecked&sortType=desc&country=US
```

A aplicação considera os 10 primeiros resultados, aceita somente protocolos HTTP/HTTPS compatíveis com o proxy do Windows e evita duplicidades. Proxies SOCKS são ignorados. Os servidores encontrados ficam disponíveis no seletor durante a sessão atual.

## O que ele faz

1. Testa o proxy selecionado contra `https://discord.com/api/v10/gateway`, com timeout de 5 segundos.
2. Tenta os outros servidores disponíveis até encontrar um válido.
3. Salva a configuração atual de proxy do usuário.
4. Ativa o proxy válido e reinicia o Discord.
5. Mantém o proxy ativo por 10 segundos.
6. Restaura exatamente a configuração anterior.

Se nenhum servidor funcionar, o proxy do Windows não é alterado. Se ocorrer um erro após a ativação, a aplicação tenta restaurar a configuração no bloco de finalização.

## Compilar

### Jeito fácil

Dê dois cliques em:

`build.bat`

O arquivo final será copiado para:

`dist\DiscordProxyLauncher.exe`

Esse é o único arquivo que você precisa.

### Visual Studio

Abra `DiscordProxyLauncher.csproj` no Visual Studio 2022 e compile em Release.

### Linha de comando

```powershell
dotnet restore
dotnet build DiscordProxyLauncher.csproj -c Release
```

O projeto utiliza .NET Framework 4.8, C# 7.3 e Windows Forms.

## Segurança e observações

Durante alguns segundos, outros aplicativos que respeitem o proxy do sistema também podem utilizar o servidor selecionado. Proxies públicos são operados por terceiros e podem ser instáveis ou inseguros; não os utilize para tráfego sensível.
