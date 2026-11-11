# 🧠 Second Brain com Wikilinks e MOCs

Este diretório contém a estrutura completa de **Personal Knowledge Management (PKM)** baseada no método **MOC (Maps of Content)**, **Zettelkasten** e **PARA**, com suporte nativo a **Wikilinks (`[[Nome da Nota]]`)** e um **Visualizador Interativo de Grafo**.

---

## 📂 Estrutura de Pastas

```text
SecondBrain/
│
├── 00_Meta/                        # Ponto de entrada e configuração do cofre
│   └── Home.md                     # Entrada principal com índices
│
├── 01_MOCs/                        # Maps of Content (Centros de navegação por tema)
│   ├── MOC - Índice Geral.md        # Hub mestre
│   ├── MOC - Projetos.md           # Hub de iniciativas ativas e ideias
│   ├── MOC - Conhecimento.md       # Hub de conceitos e frameworks mentais
│   └── MOC - Recursos & Referências.md # Hub de guias e ferramentas
│
├── 02_Atlas/                       # Conceitos teóricos perenes
│   ├── Map of Content - Conceito.md
│   ├── Wikilinks e Conexões Bidirecionais.md
│   ├── Metodologia Zettelkasten.md
│   └── Metodologia PARA.md
│
├── 03_Projects/                    # Projetos com escopo e status
│   ├── Projeto Echo.md
│   └── Second Brain System.md
│
├── 04_Cards/                       # Notas atômicas e reflexões rápidas
│   └── Nota Atômica - Densidade de Conexões.md
│
├── 05_Resources/                   # Documentação, guias e referências
│   ├── Guia de Ferramentas PKM.md
│   └── Boas Práticas de Anotação.md
│
├── graph.html                      # Visualizador de Grafo Interativo (abrir no navegador)
└── README.md                       # Este arquivo
```

---

## 🌐 Como Usar

### 1. Visualizador de Grafo Standalone (`graph.html`)
- Basta abrir o arquivo `SecondBrain/graph.html` diretamente em qualquer navegador (Chrome, Edge, Firefox).
- **Recursos**:
  - Simulação física de nós com zoom e pan.
  - Painel lateral interativo que lista **Wikilinks de Saída** e **Backlinks**.
  - Clique em qualquer nó ou link do painel para navegar instantaneamente.

### 2. No Obsidian
1. Abra o Obsidian.
2. Selecione **"Open folder as vault"** e aponte para a pasta `SecondBrain`.
3. Pressione `Ctrl + G` para ver o Graph View nativo do Obsidian com todos os nós e conexões renderizados perfeitamente.

### 3. No VS Code / Cursor (com Foam ou Markdown All in One)
1. Instale a extensão **Foam for VS Code**.
2. Abra a pasta `SecondBrain` e navegue pelas notas com `Ctrl + Clique` nos `[[Wikilinks]]`.
