# Contract: CTR-FMT-ERR-001 & CTR-FMT-ERR-002 — Format Error Handling

**Feature**: 002 — Multi-Format Audio Library  
**Date**: 2026-09-20  
**Status**: Formal Contract  

---

## 1. CTR-FMT-ERR-001: Classificação de Falhas de Extração de Metadados

O serviço `AtlMetadataService` DEVE capturar e categorizar com precisão os cenários anômalos de arquivos de áudio, retornando um DTO válido com a flag `ExtractionFailed = true` em vez de lançar exceções não tratadas para o scanner.

### 1.1 Códigos Padronizados de Erro:

| Código de Erro | Condição de Disparo | Tratamento pelo Scanner |
|:---|:---|:---|
| `"EmptyFile"` | `FileInfo.Length == 0` | Descarte definitivo; não insere no banco |
| `"UnsupportedFormat"` | ATL reporta `AudioFormat.ID == -1` ou `"Unknown"` | Descarte definitivo; registra no resumo |
| `"CorruptFile"` | ATL reporta `AudioFormat.Readable == false` ou duração 0 sem dados | Descarte definitivo; registra no resumo |
| `"ExtractionTimeout"` | ATL excede 30 segundos de parsing em arquivo grande | Tenta 1 retry de 500ms; descarta se falhar |
| `"FileAccessError"` | Exceção de I/O (`UnauthorizedAccessException`, `IOException`) | Tenta 1 retry de 500ms; descarta se falhar |

### 1.2 Regras do Contrato:
1. O método `ExtractMetadataAsync` NUNCA deve deixar vazar exceções não tratadas (`Exception`) que causem o crash da tarefa de varredura.
2. Arquivos marcados com erros definitivos (`EmptyFile`, `UnsupportedFormat`, `CorruptFile`) NÃO devem ser retentados nem salvos como entidades `Song` válidas no SQLite.

---

## 2. CTR-FMT-ERR-002: Tratamento Gracioso de Erros no Player

Se uma faixa física for corrompida no disco após ter sido indexada, o player `LibVlcAudioPlayerService` DEVE interceptar a falha de decodificação do LibVLC e notificar a aplicação de forma não fatal.

### 2.1 Regras do Contrato:
1. Se o LibVLC entrar em estado `VLCState.Error`, o player dispara o evento `ErrorOccurred(errorMessage)`.
2. A fila de reprodução (`PlaybackQueue`) não deve travar; o usuário ou a fila pode avançar para a próxima faixa com segurança.
