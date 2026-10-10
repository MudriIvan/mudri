# Mapa de projetos de {USUARIO}

<!-- Mantenha curto (uma página). Quando o assistente errar um nome ou uma
pasta, acrescente uma linha. Nunca coloque senhas, tokens ou chaves aqui. -->

## Projetos

Confirme as pastas: troque `[PASTA]` pelo caminho real no notebook.

- **Mudri / MVM / Máquina de Vendas** (fala-se "mudri", "painel", "MVM"):
  `[PASTA]\v0-mudri`, repo GitHub `MudriIvan/v0-mudri`. Painel comercial
  multi-empresa (Next.js na Vercel, Supabase, RD Station). Produção em
  mudri.com.br, cada empresa num subdomínio (tailor.mudri.com.br...).
  Push na `main` publica na Vercel na hora.
  - Memória do projeto: `docs/historico-atividades-claude.md` (pedidos
    numerados, "#491"), `docs/mvm-checklist-diario.md` e a pasta
    `implementacoes/` (implementações numeradas, "a #44").
- **Motor de WhatsApp** (fala-se "motor"): `[PASTA]\mudri-whatsapp-motor`,
  repo `MudriIvan/mudri-whatsapp-motor`. Baileys, um container por empresa
  (slots motor-mudri, motor-001...motor-100) numa VM; deploy pelo GitHub
  Actions. Mexer só com muito cuidado: é produção.
- **Assistente** (este app, fala-se "assistente" ou "{NOME}"):
  `[PASTA]\mudri`, repo `MudriIvan/mudri`.

## Empresas (clientes do painel)

Tailor, ST3 Tailor, Manuela, AI Executive Summit, MGData, Apresentação
(empresa de demonstração e testes), i9solar (Microsoft Dynamics), Edilson,
Altamira, Cloudtarget, Radar Imobiliário. Conta coringa: imudri@.

## Estrutura fora do padrão

- No `v0-mudri` a lógica de servidor fica em `app/api` e `lib`.
- O motor é outro repositório, não uma subpasta do painel.

## Regras

- Pedidos sobre a Mudri: antes de responder "onde paramos", leia o fim de
  `docs/historico-atividades-claude.md`.
- Sempre confirmar antes de qualquer coisa em produção (Vercel, Supabase,
  motor, RD Station, WhatsApp de clientes).
- Downloads e prints ficam em `C:\Users\Ivan.mudri\Downloads`.
