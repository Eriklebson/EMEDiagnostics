# Relatório de desenvolvimento

## 2026-07-21 — Estrutura inicial

- Criada a solução inicial em camadas.
- Documentado o padrão visual herdado conceitualmente do E.M.E Core.
- Preparadas navegação, injeção de dependência e telas estruturais.
- Implementada coleta real inicial via LibreHardwareMonitor, sem PowerShell.
- Preparada abstração para motores gráficos futuros.

## 2026-07-21 — Dashboard detalhada de hardware

- A Dashboard passou a apresentar um card vertical de largura total para cada hardware detectado.
- Todos os hardwares e sub-hardwares do LibreHardwareMonitor são percorridos recursivamente.
- Cada card lista os sensores disponíveis com tipo, valor atual, mínimo, máximo, unidade e identificador interno.
- Componentes sem sensores dinâmicos continuam visíveis e são identificados claramente.
- Mantida coleta direta em C#, sem PowerShell ou arquivos intermediários.
- Corrigido crash `0xc000027b` no compositor WinUI: a árvore visual deixou de ser reconstruída a cada segundo; agora somente os textos dos sensores são atualizados, e os cards são recriados apenas quando a estrutura de hardware muda.

## 2026-07-22 — Renderizador real com Sponza, PBR, bloom e sombras (em andamento)

- Baixado modelo Sponza glTF (25 malhas, centro ≈ (−0,5; 5,2; −0,3) e raio ≈ 14,9).
- Criado `RealSceneRenderer`: C++ com shaders embutidos HLSL 5.0.
- Implementados PBR Cook‑Torrance (GGX, Smith, Fresnel‑Schlick), shadow map PCF 3×3, ACES tonemapping, bloom com bright‑pass + Gaussiano separável, passada de composição final.
- Adicionados `QualitySettings` e níveis Low/Medium/High/Ultra com resolução de shadow map e bloom liga/desliga.
- Expandido `GpuStressEngine` para encaminhar qualityLevel, unbind UAV compute antes dos passes de render e modo contínuo (sem limite de tempo).
- C#: `GpuStressOptions.QualityLevel`, `DirectX11GpuStressEngine.StartAsync` aceita `TimeSpan.Zero` como contínuo, `MainViewModel` expõe qualidade, `MainWindow` com ComboBox de qualidade e status "CONTÍNUO".
- Adicionado log em arquivo (`gpu_stress_debug.log`) no `RealSceneRenderer` e `GpuStressEngine`.
- **Corrigido** `FrontCounterClockwise = TRUE → FALSE` no rasterizer da cena principal (Sponza usa winding CW, compatível com DirectX).
- **Corrigido** shadow pass: agora seta `IASetInputLayout` e `IASetPrimitiveTopology` antes de desenhar (antes usava estado da frame anterior).
- **Corrigido** ordem de execução: constant buffer atualizado **antes** do shadow pass (antes o shadow pass usava `cbScene_` não inicializado na primeira frame).
- Compilação bem‑sucedida de toda a solução (C++ via MSBuild, C# via VS MSBuild).

- Criado `EME.Diagnostics.GpuEngine`, backend nativo C++/DirectX 11 Compute.
- Implementada carga compute controlada sem dependência do FurMark.
- Adicionadas métricas de tempo, progresso, dispatches por segundo, frame time, VRAM reservada e erros.
- Adicionados cancelamento, detecção de remoção da GPU pelo driver e proteção térmica em 90 °C.
- Ativado o cartão de GPU na tela Stress Test, mantendo `IGpuStressEngine` desacoplado da UI.
- Adicionada janela 3D DirectX 11 dedicada, com cena procedural do núcleo E.M.E, três anéis animados, iluminação, estrelas e cores azul/amarela/roxa.
- Fechar a janela 3D agora encerra o teste de GPU de forma controlada.
- Substituída a demonstração do núcleo por uma cinematic medieval procedural mais pesada, com cidade, castelo, torres, telhados, lua, estrelas, tochas, janelas emissivas, sombras suaves, ambient occlusion e neblina.
- Elevada a cena para 1600×900 e adicionados materiais procedurais de pedra/telha/calçamento, BRDF PBR, fogo visível, nuvens animadas, partículas de brasa e tone mapping ACES.
- Adicionada estrada circular de terra com sulcos de rodas, pedras e variação de solo, além de uma carroça animada percorrendo a cidade.
- Baixado e auditado o Medieval Village MegaKit CC0 para a futura substituição da geometria procedural por malhas e texturas reais.

## 2026-07-22 — Terreno procedural, cena medieval na superfície, IBL com sky cubemap, sunset

- Substituído o piso plano por um **terreno gerado por heightmap** (257×257) com Perlin noise multioitava, produzindo colinas suaves e um vale central para a vila.
- **BuildTerrainMesh**: malha com subdivisão uniforme, normais calculadas por diferenças centrais no heightmap, UVs para texturização.
- **Casa, igreja e árvores** agora amostram a altura do terreno (`SampleHM`) para assentar corretamente na superfície.
- **Cercas, postes de luz, poço, bancos e barracas** também seguem o relevo.
- **Estrada principal e transversal** convertidas de retângulos planos para malhas segmentadas que acompanham o terreno.
- **Câmera da cinematic** ajustada para percorrer o terreno com altura relativa ao heightmap (caminho spline agora tem `p.y += SampleHM(...)`).
- **IBL real**: removida a função `GetSky()` procedural do pixel shader; agora a iluminação difusa e especular ambiente usa `Sky.SampleLevel` no cubemap gerado.
- **Céu sunset**: `FillSkyFace` reescrito com gradiente poente (laranja/rosado no horizonte, azul escuro no zênite) + glow dourado + disco solar.
- **Névoa baseada em altura**: `heightFog` combinada com `distFog`, cor da névoa amostrada do cubemap do céu para integração visual.
- **Bloom**: kernel Gaussiano expandido de 6 taps para 8 taps com `exp(-j²·0.15)`, intensidade aumentada para 0.12.
- **Iluminação sunset**: direção da luz ajustada para `(-0.2, -0.85, 0.45)` com cor mais quente `(1.6, 1.1, 0.5)` e intensidade 2.5.
- **Member variables** `heightmap_`, `hmRes_`, `worldSize_` adicionados à classe `VillageScene` para acesso no método `Render`.

## 2026-07-25 — Release v1.0.0.0, README, instalador Inno Setup

- README.md completo com features, arquitetura, build e sistema visual
- `installer.iss` (Inno Setup) para gerar instalador do EMEDiagnostics
- `docs/logo.ico` convertido do logo EMECore para uso no instalador
- `app.manifest` atualizado para versão 1.0.0.0
- `.gitignore` atualizado com `release/`, `installer/`, `*.zip`
- Release v1.0.0.0 criada no GitHub com artefato ZIP
