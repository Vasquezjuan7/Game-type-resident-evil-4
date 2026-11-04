# RePose: bajar los brazos de mallas estáticas (OBJ)

Herramientas en Python (solo NumPy) para cambiar la pose T o A de modelos descargados sin esqueleto
(el mercader y Tomás). Giran los brazos desde el hombro mezclando suavemente la zona del hombro, y también giran las normales.

```
python deform.py <carpeta Tools/RePose> entrada.obj salida.obj vista pivX pivY angulo x_in x_out yAxila xHueco
```

Valores usados:
- Tomás (pose T): `0.17 1.43 76 0.15 0.28 1.33 0.4`
- Mercader (pose A): `0.20 1.42 27 0.18 0.30 1.24 0.265`

- `slices.py` muestra la ocupación por alturas (para localizar la axila y el hueco entre brazo y torso).
- `view.py` y `pts.py` generan vistas PNG de frente y de lado para comprobar el resultado.
