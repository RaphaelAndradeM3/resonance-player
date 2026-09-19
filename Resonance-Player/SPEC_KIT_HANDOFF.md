# SPEC_KIT_HANDOFF.md

## Objetivo

Entregar `IDEIA.md`, `PRD.md` e a Constitution ao GitHub Spec Kit sem transformar o projeto
em dezenas de microtarefas desconectadas.

## 1. Estrutura recomendada

```text
repo/
├── IDEIA.md
├── PRD.md
├── SPEC_KIT_HANDOFF.md
└── .specify/
    └── memory/
        └── constitution.md
```

## 2. Primeira spec: baseline, não feature

Prompt sugerido:

```text
/speckit.specify

FEATURE 000 — Baseline e auditoria do fork Nagi.

META IMUTÁVEL:
Antes de implementar qualquer melhoria, comprovar o estado real do repositório Nagi usado
como base. Mapear arquitetura, projetos, DI, banco, scanner, EQ, lyrics, metadados, LibVLC e
testes. Não adicionar feature nova e não refatorar código de produto.

DEFINIÇÃO DE SUCESSO:
A solution compila e os testes executam; existe relatório verificável do que já existe e das
lacunas reais do PRD.

Leia IDEIA.md, PRD.md e .specify/memory/constitution.md.
```

## 3. Fluxo recomendado

```text
/speckit.specify
/speckit.clarify
/speckit.plan
/speckit.checklist
/speckit.tasks
/speckit.analyze
/speckit.implement
/speckit.converge
```

Não aceite `tasks.md` com mais de 3 slices por padrão nem microtarefas horizontais.

## 4. Reescrita de tasks quando houver fragmentação

```text
Reescreva tasks.md agrupando tarefas horizontais em no máximo três fatias verticais.
Cada fatia deve entregar comportamento ponta a ponta, repetir META IMUTÁVEL e terminar com
dotnet build + dotnet test. Não altere a spec para acomodar a fragmentação.
```

## 5. Feature 001 sugerida

```text
FEATURE 001 — Recursive Root Library Hardening.

META IMUTÁVEL:
Ao selecionar uma ou mais pastas raiz, toda música suportada e legível nas subpastas elegíveis
deve ser descoberta, indexada uma única vez e disponibilizada na biblioteca, sem bloquear UI
e sem falhar todo o scan por causa de um item problemático.

DEFINIÇÃO DE SUCESSO:
Árvore de teste com 3+ níveis, raízes sobrepostas, arquivo inválido e caso de reparse point é
processada deterministicamente; sem duplicatas; progresso/cancelamento/erros visíveis; build
e testes completos passam.

REGRA DE OURO:
Modificar o scanner/persistência/UI existentes. Não criar segundo scanner/biblioteca.
```

## 6. Gate .NET

```powershell
dotnet restore
dotnet build Nagi.sln --configuration Release --no-restore
dotnet test Nagi.sln --configuration Release --no-build
```

A Feature 000 deve substituir esses comandos pelos oficiais do repositório se houver
diferenças.

## 7. Prompt de recuperação de goal drift

```text
PARE a implementação local.

Releia:
1. .specify/memory/constitution.md
2. META IMUTÁVEL da spec
3. plan.md
4. código real afetado

Mostre:
- comportamento observável desta slice;
- tipos/serviços existentes reutilizados;
- arquivos redundantes criados;
- menor diff coerente para voltar ao plano.

Não escreva código adicional até reconciliar essas fontes.
```

## 8. Checklist de conclusão

```text
[ ] Meta Imutável satisfeita
[ ] <= 3 slices
[ ] Sem arquitetura paralela
[ ] Build da solution passa
[ ] Testes passam
[ ] Teste de fluxo relevante passa
[ ] License/provider gate revisado
[ ] Offline/local-first preservado
[ ] Logs sem secrets
[ ] Spec/PRD atualizados se comportamento mudou
[ ] /speckit.converge sem bloqueador
```
