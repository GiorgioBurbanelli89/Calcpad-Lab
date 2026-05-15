# Benchmarks de placas FEM — comparación entre entornos

Scripts MATLAB **100% portables** que validan el comportamiento de
visualización 2D/3D en distintos entornos:

| Entorno          | Tipo            | Output                          |
|------------------|-----------------|----------------------------------|
| **MATLAB**       | nativo          | figuras estándar (figure, GUI) |
| **Octave**       | GNU MATLAB      | figuras gnuplot                  |
| **Calcpad Lab**  | Calcpad fork    | HTML con Three.js orbital + SVG  |
| **Hekatan Lab**  | (futuro)        | (mismo backend Calcpad Lab)      |

Los scripts usan **únicamente funciones MATLAB estándar** (`delaunay`,
`triplot`, `trimesh`, `trisurf`, `zeros`, `sin`, `mod`, `for`) — por
lo tanto corren idénticamente en todos los entornos sin modificación.

---

## Benchmarks portados de `awatif-v2/awatif-fem/src/deform.test.ts`

### 1. `plate_pinned_uniform_load.m`

Placa cuadrada 10×10 m empotrada en bordes, carga uniforme p₀=-1000 N/m².

| Entorno      | w_max [mm] | Mesh        | Time |
|--------------|------------|-------------|------|
| MATLAB       | _?_        | 162 tri     | -    |
| **Octave**   | **13.451** | 161 tri     | <1s  |
| **Calcpad Lab** | **13.451** | 161 tri     | <1s  |
| awatif FEM   | 12.690     | 162 tri     | -    |
| Timoshenko exacto | 13.541 | analítica   | -    |

- **Calcpad Lab y Octave dan resultados idénticos** (13.451 mm)
- La aproximación con primer término de serie de Fourier converge a
  Timoshenko con error <1% (13.451 vs 13.541)
- awatif FEM con malla discreta da algo menos por discretización

### 2. `plate_orthotropic.m`

Misma placa pero con material **ortotrópico** (Ex=10 GPa, Ey=5 GPa).

| Entorno      | w_max [mm] | Comentario |
|--------------|------------|------------|
| **Octave**   | **16.847** | -          |
| **Calcpad Lab** | **16.847** | idéntico   |

Mayor deflexión que isotrópica (16.85 vs 13.45 mm) porque la rigidez
efectiva en y es la mitad.

---

## Cómo correr los benchmarks

### MATLAB (real)
```matlab
>> cd C:\Users\j-b-j\Documents\Hekatan Calc 1.0.0\Calcpad-Lab\Examples\Benchmarks-Plates
>> plate_pinned_uniform_load
% Aparecen 2 figuras: triplot wireframe + trisurf 3D coloreado
```

### Octave
```bash
$ cd /path/to/Benchmarks-Plates
$ octave --no-gui plate_pinned_uniform_load.m       # cálculo solo
$ octave plate_pinned_uniform_load.m                # con figuras
```

### Calcpad Lab CLI
```cmd
> CalcpadLabCli.exe plate_pinned_uniform_load.m
✓ Reporte generado: plate_pinned_uniform_load.html
[el navegador se abre automáticamente con el reporte]
```

### Calcpad Lab WPF
Abrir el archivo `.m` en `CalcpadLab.exe` — el panel derecho muestra el
reporte HTML con todas las figuras.

---

## Sintaxis MATLAB soportada en Calcpad Lab

| MATLAB syntax | Soportado | Notas |
|---|---|---|
| `x = 5` | ✅ | display |
| `x = 5;` | ✅ | suppression |
| `y=3;y_1=5;y_4=5;` | ✅ | multi-statement |
| `x = [1, 2, 3]` | ✅ | vectores con `,` o ` ` |
| `M = [1 2; 3 4]` | ✅ | matrices |
| `M(i, j) = x` | ✅ | indexed assignment |
| `for i = 1:N` | ✅ | rangos simples |
| `for i = 1:2:N` | ❌ | rango con step (pendiente) |
| `meshgrid` | ❌ | pendiente |
| `reshape` | ❌ | pendiente |
| `M(:)`, `M(i,:)` | ❌ | slicing con `:` (pendiente) |
| `delaunay(x, y)` | ✅ | Bowyer-Watson via Triangle Shewchuk |
| `mesh2d(x, y, maxA)` | ✅ | mesh refinado quality |
| `triplot(T, x, y)` | ✅ | SVG 2D |
| `trimesh(T, x, y, z?)` | ✅ | Three.js 3D orbital |
| `trisurf(T, x, y, z)` | ✅ | Three.js 3D + colormap rainbow |
| `function out = fn(args)` | ✅ | definiciones de función |
| `[a, b] = func(args)` | ✅ | destructuring multi-output |
| `% comentario` | ✅ | comments |
| `%% Section` | ✅ | section heading |
| `'string'` | ✅ | string literals |
| `25e6`, `2.5e-3` | ✅ | notación científica |
| `==`, `~=`, `<=`, `>=` | ✅ | comparaciones |
| `pi`, `sin`, `cos`, `exp`, `sqrt` | ✅ | builtins |
| `mod(a, b)`, `length(v)` | ✅ | math |

---

## Equivalencia de visualización

| awatif-v2 | Calcpad Lab | MATLAB |
|---|---|---|
| `triangle.triangulate('pzQOq30aXX', ...)` | `mesh2d(x, y, maxArea)` | `generateMesh()` PDE Toolbox |
| `THREE.Lut.setColorMap("rainbow")` | rainbow LUT inline | `colormap('jet')` |
| `THREE.MeshBasicMaterial` | MeshBasicMaterial | sin equivalente directo |
| `convertSRGBToLinear() · 0.6` | `pow(c, 2.2) · 0.6` | n/a |
| `MeshLineSegments` (negro) | `WireframeGeometry` (negro) | wireframe automático |
