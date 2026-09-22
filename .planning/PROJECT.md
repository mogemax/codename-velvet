# codename-velvet (working title)

## What This Is

Aventura/acción cooperativa 2.5D para hasta 5 jugadores vía Steam Remote
Play Together, ambientada en una reinterpretación de La Odisea con
personajes animalitos. El loop combina recolección y crafteo (referencia
Minecraft/Valheim) con caos nocturno (referencia MUCK) y progresión de
nivel por reparación de barco (referencia Raft). v1 cubre el Nivel 1
completo: isla inicial, ciclo día/noche, enemigos regulares y el cíclope
como boss.

## Core Value

El loop día/noche cooperativo debe funcionar: de día se recolecta y
construye con calma, de noche aumenta el caos y la presión. Si ese ciclo
no genera tensión y diversión jugando en grupo, nada más del proyecto
importa.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] Player controller: input, movimiento, salto, maroma/roll
- [ ] Recolección de recursos (madera/piedra/mineral) con herramientas por tier
- [ ] Crafteo básico (mecánica de detalle pendiente — ver Context)
- [ ] Ciclo día/noche con aumento de amenaza nocturna
- [ ] Enemigos regulares (minotauro, sirena, bruja) con IA básica
- [ ] Boss del cíclope (fases, cuida un ítem específico)
- [ ] Reparación de barco para avanzar de nivel
- [ ] Multiplayer local vía Steam Remote Play Together (hasta 5 jugadores)
- [ ] UI: HUD, inventario, menú de crafteo, indicador día/noche, barra de vida de boss

### Out of Scope

- Nivel 2 (sirenas como enemigos de nivel avanzado, boss dragón/drakon) — diferido a v2, ver Key Decisions
- Diseño final de tiers de armas vs herramientas — el usuario aún no definió esta mecánica, no planear esa fase hasta resolverlo
- Netcode propio — el juego se apoya enteramente en Steam Remote Play Together, no hay sincronización de red a implementar

## Context

- Género: aventura/acción cooperativa multijugador (hasta 5 jugadores), plataforma Steam.
- Multiplayer exclusivamente vía Steam Remote Play Together — no hay networking propio que implementar; todos los jugadores comparten una sola instancia local.
- Estilo visual 2.5D: personajes en sprite 2D sobre escenario 3D (referencia: Cult of the Lamb).
- Referencias de gameplay: MUCK (caos nocturno) + progresión de herramientas tipo Minecraft/Valheim + estructura de avance tipo Raft (reparar y navegar a la siguiente zona).
- Loop principal: isla → recolectar recursos → craftear herramientas/armas → sobrevivir la noche → reparar el barco → avanzar de isla → boss final del nivel.
- El usuario ya definió una estructura de código propia de ~50 scripts organizados en 11 dominios: Player, Inventario/Ítems, Recolección, Crafteo, Ciclo Día/Noche, Enemigos, Boss, Barco/Progresión, Multijugador, Core/Manejo General, UI. Esta estructura debe respetarse al planear fases y generar código.
- El proyecto Unity (`codename-velvet`, Unity 6000.3.8f1) está inicializado pero vacío — `Assets/Scripts` no tiene ningún archivo todavía.

## Constraints

- **Plataforma**: Steam — sin netcode propio; todo corre en una instancia local compartida vía Remote Play Together.
- **Estilo visual**: 2.5D (sprites 2D + escenario 3D) — condiciona cómo se implementan Animator, cámara y colisiones.
- **Jugadores**: hasta 5 simultáneos, input local multi-dispositivo (Unity Input System).
- **Motor**: Unity 6 (6000.3.8f1).
- **Estructura de código**: seguir los 11 dominios y nombres de archivo ya definidos por el usuario, no reinventar la organización.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| v1 = Nivel 1 completo, Nivel 2 diferido | Reducir alcance para tener un vertical slice completo y jugable antes de sumar más contenido | — Pending |
| Maroma híbrida: cooldown corto, sin i-frames | Sirve para traversal y comedia física, pero no es un esquive confiable en combate serio | — Pending |
| Inventario: stack por tipo | 3-4 slots con stackeo evita que el inventario se llene al instante con tan pocos slots | — Pending |
| Detalle de tiers de crafteo (armas vs herramientas) diferido | El usuario todavía no definió esta mecánica en profundidad; no bloquea el resto del roadmap | — Pending |
| Skills router del proyecto acoplado al global (`~/.claude/skills-router.md`) | Unity/C# solo usa `ponytail-review` y `full-output-enforcement`; evita duplicar reglas | ✓ Good |
| Planificación vía GSD | Roadmap fase por fase para los ~50 scripts en 11 dominios | — Pending |

---
*Last updated: 2026-09-22 after initialization*
