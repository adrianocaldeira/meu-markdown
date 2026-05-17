# Política de Segurança

## Versões suportadas

Apenas a última versão minor recebe correções de segurança.

| Versão | Suportada |
|---|---|
| 1.1.x  | ✅ |
| < 1.1  | ❌ |

## Reportar uma vulnerabilidade

**Por favor, não abra issues públicas para vulnerabilidades.**

Use o canal privado do GitHub:

1. Acesse https://github.com/adrianocaldeira/meu-markdown/security/advisories/new
2. Descreva a falha, impacto e passos de reprodução

Você receberá uma resposta em até **5 dias úteis**. Se a vulnerabilidade for confirmada:

- Reconhecemos o reporte
- Combinamos prazo para disclosure
- Publicamos correção em release patch
- Damos crédito ao reporter (se desejar) no advisory

## Escopo

O Meu Markdown é um app Windows local que abre arquivos `.md` do disco. Os principais vetores que monitoramos:

- Manipulação do conteúdo Markdown que cause execução de código (XSS no preview, escape de sandbox WebView2)
- Path traversal via links `.md`
- Persistência de estado (`%AppData%/MeuMarkdown/state.json`) sendo desserializada de forma insegura
- Vulnerabilidades em dependências (AvalonEdit, Markdig, WebView2)

## Fora de escopo

- Falhas em runtimes/SO de terceiros (Windows, .NET runtime, WebView2 runtime do Edge)
- Engenharia social
- Vulnerabilidades em forks
