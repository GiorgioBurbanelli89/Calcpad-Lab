%% Viga Euler-Bernoulli: del cálculo SIMBÓLICO al resultado con NÚMEROS
%' <h3>Rigidez de viga: deducción simbólica → resultado numérico</h3>
%' <hr/>
%' Deducimos la matriz de rigidez de una viga con álgebra simbólica y luego la
%' evaluamos con valores reales para calcular una deflexión. El MISMO motor hace
%' el símbolo y el número, sin salir del reporte y sin usar disp/fprintf.

%' <h4>1. Funciones de forma de Hermite (deducidas)</h4>
syms x L real
s = x/L;
N1 = 1 - 3*s^2 + 2*s^3;      % flecha en el nudo 1
N2 = x*(1 - s)^2;            % giro en el nudo 1
N3 = 3*s^2 - 2*s^3;          % flecha en el nudo 2
N4 = x^2/L*(s - 1);          % giro en el nudo 2
N = [N1, N2, N3, N4];
%' Funciones de forma  N = [N_1  N_2  N_3  N_4]:
N
%' Sus curvaturas (segundas derivadas).  N'' se escribe con el token `Npprime`:
Npprime = simplify(diff(N, x, 2))

%' <h4>2. Matriz de rigidez SIMBÓLICA</h4>
%' Integrando el producto de curvaturas por la rigidez a flexión EI (constante),
%' la propia integral se renderiza al mostrar el resultado:
syms EI real
K_sym = simplify(EI * int(Npprime.' * Npprime, x, 0, L))

%' <h4>3. Ahora con VALORES</h4>
% Datos (ocultos): E en kN/m², I en m⁴, L en m
E = 210e6;  Iz = 8.333e-6;  Lval = 4;
%' Módulo  E = @E kN/m² ,  inercia  I = @Iz m⁴ ,  longitud  L = @Lval m
%' Sustituimos EI y L en la matriz simbólica → matriz de rigidez NUMÉRICA:
K = double(subs(K_sym, {EI, L}, {E*Iz, Lval}))

%' <h4>4. Deflexión de un voladizo con carga P en la punta</h4>
P = 10;   % kN (oculto)
%' Empotrado en el nudo 1 (gdl w_1, θ_1 fijos); carga  P = @P kN hacia abajo en el nudo 2.
Kff = K(3:4, 3:4);            % gdl libres: w_2, θ_2
F = [-P; 0];                 % fuerza en w_2 (sin momento)
d = Kff \ F;                 % desplazamientos libres
w2_mm = d(1)*1000            %' Deflexión en la punta  w_2 = @ mm
%' Fórmula clásica del voladizo  P·L³/(3·E·I), para comparar:
w_teo_mm = -P*Lval^3/(3*E*Iz)*1000   %' teórica  = @ mm
%" El símbolo se convierte en número — y coincide con la teoría.
