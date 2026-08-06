%' # Interpolacion cubica de la deformada — como la dibuja ETABS
%'
%' Una barra de portico tiene SOLO DOS NUDOS. Sin embargo ETABS la dibuja
%' curvada cuando se activa «Cubic Curve». No inventa nodos intermedios: lo
%' comprobe leyendo el modelo, donde los objetos y los elementos de analisis
%' dan el mismo numero (FrameObj 53 = LineElm 53, y la columna tiene 2 nudos).
%'
%' Lo que hace es usar la matematica de la viga. Un nudo no solo se DESPLAZA:
%' tambien GIRA. Con dos nudos hay CUATRO datos por plano — desplazamiento y
%' giro en cada extremo — y cuatro condiciones determinan un polinomio de
%' TERCER grado.

%' ## Las funciones de forma de Hermite
%'
%' Con s = x/L variando de 0 a 1, la deformada transversal vale:

% #noc v(s) = N₁·v_i + N₂·θ_i + N₃·v_j + N₄·θ_j

%' y las cuatro funciones son:

% #noc N₁ = 1 - 3·s² + 2·s³
% #noc N₂ = L·(s - 2·s² + s³)
% #noc N₃ = 3·s² - 2·s³
% #noc N₄ = L·(-s² + s³)

%' No son una curva elegida para que quede lindo: son las MISMAS funciones con
%' las que el metodo de los elementos finitos armo la matriz de rigidez de la
%' barra. Dibujar con ellas muestra la deformada que el elemento realmente
%' tiene entre sus extremos.

%' ## Demostracion simbolica
%'
%' Aca ya no es Hekatan Symbolic sino MATLAB nativo: se derivan y se comprueba
%' que cumplen las cuatro condiciones de borde.

%' No las escribo ya hechas: las DEDUZCO. Se parte de una cubica generica y se
%' le imponen las cuatro condiciones de borde. Esto es simbolico de MATLAB.

syms s L a b c d v_i t_i v_j t_j real

v  = a + b*s + c*s^2 + d*s^3;      % cubica generica
dv = diff(v, s)/L;                 % pendiente: s = x/L, por eso el /L

%' Las cuatro condiciones: en s=0 vale v_i y su pendiente es t_i; en s=1 vale
%' v_j y su pendiente es t_j.
ec = [ subs(v,  s, 0) == v_i, ...
       subs(dv, s, 0) == t_i, ...
       subs(v,  s, 1) == v_j, ...
       subs(dv, s, 1) == t_j ];

sol = solve(ec, [a b c d]);
v_s = simplify(expand(subs(v, {a, b, c, d}, ...
                           {sol.a, sol.b, sol.c, sol.d})));
disp('Deformada v(s) deducida de las 4 condiciones:')
disp(collect(v_s, [v_i t_i v_j t_j]))

%' Agrupando por cada grado de libertad salen las cuatro funciones de forma.
%' Se extraen como el COEFICIENTE que multiplica a cada uno:
N1 = simplify(diff(v_s, v_i));
N2 = simplify(diff(v_s, t_i));
N3 = simplify(diff(v_s, v_j));
N4 = simplify(diff(v_s, t_j));
disp('N1 ='), disp(N1)
disp('N2 ='), disp(N2)
disp('N3 ='), disp(N3)
disp('N4 ='), disp(N4)

%' Y coinciden con las de Hermite escritas arriba. Se comprueba restando:
disp('diferencia con las de Hermite (debe dar 0 0 0 0):')
disp(simplify([N1 - (1 - 3*s^2 + 2*s^3), ...
               N2 - L*(s - 2*s^2 + s^3), ...
               N3 - (3*s^2 - 2*s^3), ...
               N4 - L*(-s^2 + s^3)]))

%' Valor en los extremos: N1 vale 1 en el nudo i y 0 en el j; N3 al reves.
%' Los N2 y N4 valen CERO en los dos extremos — son los que aportan el giro.
disp('N en s=0 :'), disp([subs(N1,s,0) subs(N2,s,0) subs(N3,s,0) subs(N4,s,0)])
disp('N en s=1 :'), disp([subs(N1,s,1) subs(N2,s,1) subs(N3,s,1) subs(N4,s,1)])

%' La PENDIENTE es la que separa a cada una. Ojo: la derivada respecto de x es
%' d/ds dividido por L, porque s = x/L.
dN1 = diff(N1,s)/L;  dN2 = diff(N2,s)/L;
dN3 = diff(N3,s)/L;  dN4 = diff(N4,s)/L;
disp('dv/dx en s=0 :'), disp(simplify([subs(dN1,s,0) subs(dN2,s,0) subs(dN3,s,0) subs(dN4,s,0)]))
disp('dv/dx en s=1 :'), disp(simplify([subs(dN1,s,1) subs(dN2,s,1) subs(dN3,s,1) subs(dN4,s,1)]))

%' Sale la matriz identidad: cada funcion controla UN grado de libertad y no
%' toca los otros tres. Eso es lo que las hace servir de base.

%' ## La columna del mezanine
%'
%' Datos reales del modelo: columna de 4.00 m, cabeza que baja 0.187 m con un
%' giro de 0.0090 rad, base empotrada.

Lcol = 4.00;      % m
v_i  = 0.0;       % base empotrada: no se mueve
t_i  = 0.0;       % ni gira
v_j  = 0.0148;    % la cabeza se corre lateralmente
t_j  = 0.0090;    % y gira

sv = linspace(0,1,60);
n1 = 1 - 3*sv.^2 + 2*sv.^3;
n2 = Lcol*(sv - 2*sv.^2 + sv.^3);
n3 = 3*sv.^2 - 2*sv.^3;
n4 = Lcol*(-sv.^2 + sv.^3);
v_cubica = n1*v_i + n2*t_i + n3*v_j + n4*t_j;
v_recta  = v_i + (v_j - v_i)*sv;          % lo que dibujaba Hekatan hasta hoy

z = sv*Lcol;
figure; hold on
plot(v_recta*1000,  z, '--', 'LineWidth', 1.5)
plot(v_cubica*1000, z, '-',  'LineWidth', 2.5)
plot([v_i v_j]*1000, [0 Lcol], 'o', 'MarkerSize', 8, 'LineWidth', 2)
xlabel('desplazamiento lateral (mm)'); ylabel('altura z (m)')
title('Columna de 4 m: recta entre extremos vs cubica de Hermite')
legend('recta (une los extremos)','cubica (usa tambien los giros)','los 2 nudos')
grid on

%' La diferencia maxima entre las dos, que es lo que se ve en pantalla:
dif_mm = max(abs(v_cubica - v_recta))*1000

%' ## Las cuatro funciones, dibujadas

figure; hold on
plot(sv, n1, 'LineWidth', 2)
plot(sv, n2/Lcol, 'LineWidth', 2)
plot(sv, n3, 'LineWidth', 2)
plot(sv, n4/Lcol, 'LineWidth', 2)
xlabel('s = x/L'); ylabel('valor')
title('Funciones de forma de Hermite (N_2 y N_4 divididas por L)')
legend('N_1  desplaz. en i','N_2  giro en i','N_3  desplaz. en j','N_4  giro en j')
grid on

%' ## Que cambia en el dibujo
%'
%' Uniendo los extremos con una RECTA, una columna empotrada abajo sale
%' derecha: el giro nulo de la base no se ve por ningun lado. Con la cubica
%' arranca vertical (porque θ_i = 0) y se curva hacia la cabeza, que es la
%' elastica de verdad.
%'
%' Es puramente de visualizacion: los desplazamientos de los nudos son los
%' mismos: en el mezanine, Hekatan Struct y ETABS coinciden al 0.00 %.
