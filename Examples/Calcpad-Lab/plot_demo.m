%% Calcpad Lab — Demo de gráficos MATLAB-style
% Demuestra plot/title/xlabel/etc. mapeados a Calcpad nativo $Plot

%% Curva sinusoidal
x = vector(50)
% Llenar x con 0 : 2π en 50 pasos
% (en v1 sin loops imperativos usamos rangos directo en $Plot)

%% Función analítica plotted (esto SÍ funciona)
$Plot{ sin(x) @ x = 0 : 6.28 }

%% Función segunda
$Plot{ cos(x) @ x = 0 : 6.28 }

%% Dos curvas juntas
$Plot{ sin(x) & cos(x) @ x = 0 : 6.28 }

%% Función polinómica
$Plot{ x^2 - 4*x + 3 @ x = -2 : 6 }

%% Mapa 3D (contour)
$Map{ sin(x) * cos(y) @ x = 0 : 6.28 & y = 0 : 6.28 }

%% Resultado: render embebido en HTML con gráficos SVG inline (CSS puro, sin JS)
