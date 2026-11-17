---
name: design
description: Use ao criar ou revisar interfaces (sites, apps, dashboards). Ajuda a definir tipografia, paleta de cores, espaçamento, hierarquia visual e acessibilidade com decisões consistentes.
---

# Skill de Design

Ao trabalhar em qualquer interface, siga este processo antes de escrever código de estilo.

## 1. Entenda o contexto primeiro
Antes de propor estilos, identifique:
- **Público e tom**: corporativo, criativo, técnico, infantil?
- **Plataforma**: web, mobile, dashboard?
- **Restrições**: já existe marca/cores? precisa seguir um design system?

Se faltar informação essencial, pergunte em vez de assumir.

## 2. Sistema de espaçamento
Use uma escala baseada em 4px (ou 8px para layouts maiores):
`4, 8, 12, 16, 24, 32, 48, 64, 96`
Nunca use valores aleatórios como `13px` ou `27px`.

## 3. Tipografia
- Escolha no máximo **2 famílias** (uma para títulos, uma para corpo).
- Defina uma escala clara de tamanhos, ex.: `12, 14, 16, 20, 24, 32, 48`.
- Altura de linha: ~1.5 para corpo, ~1.2 para títulos.
- Limite a largura de leitura a ~65–75 caracteres por linha.

## 4. Cor
- Comece por uma cor primária e derive neutros (cinzas) a partir dela.
- Use no máximo 1 cor de destaque (accent) além da primária.
- Garanta contraste mínimo **4.5:1** para texto (WCAG AA).
- Estados: hover, active, disabled, focus — todos precisam existir.

## 5. Hierarquia e layout
- Um elemento deve ser claramente dominante por tela.
- Use espaço em branco para agrupar, não bordas em tudo.
- Alinhe elementos a uma grade; evite centralizar texto longo.

## 6. Checklist antes de entregar
- [ ] Contraste de texto ≥ 4.5:1
- [ ] Foco visível em elementos interativos (teclado)
- [ ] Espaçamentos da escala (sem valores soltos)
- [ ] Máximo 2 fontes e 2 cores de destaque
- [ ] Estados de hover/focus/disabled definidos
- [ ] Responsivo: testado em ~375px e ~1440px

## Referências
- `references/cores.md` — paletas e regras de contraste
- `references/tipografia.md` — pares de fontes e escalas
