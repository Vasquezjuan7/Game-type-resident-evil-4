# Checklist QA

## General
- [ ] Teclado, ratón y mando funcionan en todas las pantallas; iconos cambian según dispositivo.
- [ ] Cada pantalla abre con una opción seleccionada; Esc/B siempre regresa.
- [ ] Fuentes correctas, tildes y ñ visibles.
- [ ] Se ve bien en 16:9, 16:10 y 21:9.
- [ ] "Reducir movimiento" apaga grano, parpadeo, empujes de cámara y partículas.
- [ ] 60 FPS estables en menús.

## Menú principal y modo cine
- [ ] Las 3 tomas rotan cada 10 s con crossfade; los zombies casi no se mueven.
- [ ] Logo con efecto eco.
- [ ] Continuar deshabilitado sin memoria guardada.
- [ ] Modo cine entra a los 20 s y sale con cualquier input sin ejecutar acción.

## Nueva partida
- [ ] Cambiar dificultad actualiza el panel de efectos.
- [ ] Guardar progreso asigna una memoria falsa, muestra el toast y habilita Continuar.
- [ ] Comenzar → pantalla de carga → juego.

## Carga
- [ ] Mínimo 2 s, consejo aleatorio, fade al terminar.
- [ ] Nubes, niebla y ceniza animadas sin saltos.

## Opciones
- [ ] Todos los valores de Gráficos y Audio se aplican y se guardan.
- [ ] ◆ en valores modificados; R y T restablecen.
- [ ] Confirmación de 15 s al cambiar resolución.
- [ ] Idioma cambia en vivo.

## Pausa e inventario
- [ ] El juego se detiene (timeScale 0) y la UI sigue animada.
- [ ] Inventario: mover, rotar, combinar y ordenar funcionan; la ficha se actualiza.

## Créditos y salir
- [ ] Nombres cayendo en loop hasta el pie de página.
- [ ] Salir pide confirmación con "No" por defecto.
