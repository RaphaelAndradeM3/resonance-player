# Quickstart: 006 — Metadata Review, Tag Editor & File Update

**Branch**: `006-metadata-review-tag-editor`  
**Date**: 2026-09-22  
**Status**: Ready for Implementation  
**Spec Reference**: [spec.md](./spec.md) | [plan.md](./plan.md)

---

## 1. Pré-Requisitos e Ambiente

1. .NET 10.0 SDK instalado (`dotnet --version`).
2. Visual Studio 2022+ ou VS Code com extensão C# Dev Kit.
3. Windows 10/11 x64 para execução da aplicação WinUI 3.

---

## 2. Cenários de Validação Automatizada (.NET CLI)

### 2.1. Compilação da Solução com Warning as Error

```powershell
dotnet restore Resonance.slnx
dotnet build Resonance.slnx --configuration Release -p:Platform=x64 --warnaserror
```
**Resultado Esperado**: Compilação limpa de todos os projetos (`Resonance.Core`, `Resonance.WinUI`, `Resonance.Core.Tests`) sem nenhum aviso.

---

### 2.2. Execução da Suite de Testes de Diff e Gravação Segura

```powershell
dotnet test tests/Resonance.Core.Tests/Resonance.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~TagDiff|FullyQualifiedName~TagWriter"
```
**Resultado Esperado**: Todos os testes unitários passando, cobrindo:
- Cálculo de diff entre `TrackAudioTags` e `EnrichmentProposal`.
- Seleção e deseleção de campos (`TagDiffRecord.IsSelected`).
- Round-trip de gravação atômica em arquivos de teste MP3 e FLAC.
- Preservação do arquivo original e exclusão do temporário em falha simulada de I/O.
- Remoção temporária de flag *Read-Only* em arquivo protegido durante a gravação atômica.

---

## 3. Cenários de Validação Manual na Aplicação WinUI

### Cenário 1: Revisão e Aplicação de Enriquecimento Online (P1)
1. Inicie o player e navegue para a biblioteca.
2. Abra o painel lateral **Track Inspector** em qualquer música.
3. Clique em **"Identificar / Buscar Metadados"** (Feature 005) para gerar a proposta.
4. Quando a proposta carregar, clique no botão **"Revisar e Gravar Tags"**.
5. No `TagEditorDialog` aberto:
   - Verifique que as colunas mostram lado a lado o valor Atual vs Sugerido.
   - Desmarque a checkbox do campo "Ano".
   - Clique em **"Gravar Alterações"**.
6. **Verificação**:
   - O diálogo fecha e exibe notificação de sucesso.
   - O painel Track Inspector atualiza imediatamente com as novas tags gravadas, exceto o "Ano" que permaneceu inalterado.
   - Abrindo o arquivo físico em leitor externo (ex: TagLib ou propriedades do Windows), confirma-se que as tags foram fisicamente gravadas.

---

### Cenário 2: Edição Manual de Tags (P2)
1. Clique com o botão direito sobre qualquer música na lista da biblioteca e selecione **"Editar Tags"** (ou acione pelo botão no Track Inspector).
2. O formulário do `TagEditorDialog` surge com os campos atuais preenchidos.
3. Modifique o título da música (ex: adicione ` (Remastered)`) e o número da faixa.
4. Clique em **"Gravar Alterações"**.
5. **Verificação**:
   - O arquivo em disco é atualizado atomicamente.
   - O banco de dados do Resonance reflete a alteração instantaneamente sem necessidade de re-escanear pastas.

---

### Cenário 3: Resiliência em Faixa em Reprodução (P3)
1. Inicie a reprodução de uma música.
2. Com a música tocando, abra o `TagEditorDialog` dessa mesma música.
3. Altere o gênero ou comentário e clique em **"Gravar Alterações"**.
4. **Verificação**:
   - A reprodução sofre apenas uma pausa quase imperceptível para a troca segura do ponteiro do arquivo e retoma automaticamente na mesma posição temporal.
   - Zero corrupção ou erro de *Sharing Violation*.

---

### Cenário 4: Arquivo com Atributo "Somente Leitura" (Read-Only)
1. No Windows Explorer, clique com o botão direito em um arquivo de áudio de teste -> Propriedades -> Marque **"Somente Leitura"** (*Read-Only*) -> OK.
2. No Resonance Player, abra o editor de tags dessa música e tente alterar o título.
3. Confirme a gravação.
4. **Verificação**:
   - O sistema remove a flag de somente-leitura e conclui a substituição atômica com sucesso.
