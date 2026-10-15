# CardDuel

Juego de cartas 3D estilo duelo para PC y móvil.

- **Motor:** Unity 6 (6000.4.4f1) · Universal Render Pipeline (perfiles PC y Mobile)
- **Plataformas:** Windows, Android (iOS y WebGL más adelante)

## Estructura

```
Assets/_Game/
  Scripts/
    Cards/      CardData, DeckData (cartas y mazos como datos), CardView (carta 3D animada)
    Duel/       DuelManager, TurnPhase (flujo de turnos y fases)
    Prototype/  Prototipo jugable: tablero, mano, colocación de cartas
  Resources/    CardText.shader (texto 3D que respeta la profundidad)
```

## Prototipo

Abre `Assets/Scenes/SampleScene` y pulsa **Play**: el prototipo se monta solo
(`PrototypeBootstrap`). Funciona con ratón y con pantalla táctil.

- Se baraja el mazo y robas 4 cartas a la mano.
- Toca una carta de la mano → elige acción → toca una zona iluminada.
  - Monstruos: invocar en ataque o colocar boca abajo en defensa
    (1 Invocación Normal por turno; nivel 5-6 pide 1 tributo, nivel 7+ pide 2).
  - Mágicas: activar (se resuelve y va al cementerio) o colocar boca abajo.
  - Trampas: colocar boca abajo (se pueden activar a partir del turno siguiente).
- Toca un monstruo en el campo para cambiar su posición o hacer una Invocación por volteo.
- Toca tu mazo para robar una carta, o pulsa *Siguiente turno*.

## Crear cartas

En Unity: `Assets > Create > CardDuel > Card Data`.
