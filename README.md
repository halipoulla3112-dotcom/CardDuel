# CardDuel

Juego de cartas 3D estilo duelo para PC y móvil.

- **Motor:** Unity 6 (6000.4.4f1) · Universal Render Pipeline (perfiles PC y Mobile)
- **Plataformas:** Windows, Android (iOS y WebGL más adelante)

## Estructura

```
Assets/_Game/Scripts/
  Cards/   CardData, DeckData (ScriptableObjects: cartas y mazos como datos)
  Duel/    DuelManager, TurnPhase (flujo de turnos y fases)
```

## Crear cartas

En Unity: `Assets > Create > CardDuel > Card Data`.
