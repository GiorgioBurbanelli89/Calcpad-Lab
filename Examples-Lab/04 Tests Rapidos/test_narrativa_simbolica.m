%% Deduccion paso a paso: viga simplemente apoyada
% Demuestra como combinar texto descriptivo con operaciones simbolicas
% para producir un cuadernillo tipo libro de mecanica estructural.

%% Planteo del problema
% Consideremos una viga de luz L empotrada simplemente en ambos extremos,
% sometida a una carga distribuida uniforme q sobre toda su luz.
% Queremos deducir simbolicamente:
%   1. La distribucion del cortante V(x)
%   2. La distribucion del momento flector M(x)
%   3. La pendiente y la deflexion y(x) usando la ecuacion EI*y'' = -M

syms x q L E I

%% Paso 1: cortante en funcion de x
% Por equilibrio, las reacciones en los apoyos son R = q*L/2 cada una.
% El cortante a una distancia x del apoyo izquierdo es la reaccion
% menos la carga acumulada de 0 a x:

V = q*L/2 - q*x

%% Paso 2: momento por integracion del cortante
% Usando la relacion dM/dx = V, integramos el cortante de 0 a x.
% La constante de integracion es cero porque M(0) = 0 en el apoyo.

M = int(V, x)

%% Paso 3: momento maximo
% Por simetria, el momento maximo ocurre en el centro de la luz, x = L/2.
% Sustituimos:

M_max = subs(M, x, L/2)

%% Comparacion con la formula clasica
% El resultado anterior debe coincidir con la formula cerrada
% conocida del Manual del Ingeniero Civil: M_max = q*L^2/8

M_max_clasico = q*L^2/8

%% Paso 4: pendiente y deflexion
% La curvatura de la viga obedece la relacion de Euler-Bernoulli:
%     EI * y'' = -M(x)
% Integrando dos veces obtenemos primero la pendiente theta y luego la
% deflexion y(x). Las constantes de integracion se determinan con las
% condiciones de borde y(0) = 0, y(L) = 0.

theta = int(-M, x) / (E*I)
y     = int(theta, x)

%% Paso 5: deflexion maxima teorica
% Resolviendo las constantes obtenemos la formula clasica:
%     y_max = 5 * q * L^4 / (384 * E * I)
% (Ver Timoshenko & Gere, "Mechanics of Materials" Tabla 9.1)

y_max_clasico = 5*q*L^4 / (384*E*I)

%% Aplicacion numerica
% Apliquemos el resultado a un caso real: una viga IPE 160 simplemente
% apoyada de 6 metros, con carga uniforme de 10 kN/m (peso propio + uso).
%   Acero ASTM A36: E = 210 GPa
%   IPE 160:         I = 869 cm^4 = 869e-8 m^4

q_num = 10000;        % 10 kN/m = 10000 N/m
L_num = 6;            % 6 m
E_num = 210e9;        % 210 GPa = 210e9 Pa
I_num = 869e-8;       % 869 cm^4 = 869e-8 m^4

%% Resultados con valores reales
M_max_real = subs(M_max_clasico, {q, L}, {q_num, L_num})
y_max_real = subs(y_max_clasico, {q, L, E, I}, {q_num, L_num, E_num, I_num})

%% Conversion a unidades practicas
% M en kN*m y deflexion en milimetros
M_max_kNm = M_max_real / 1000
y_max_mm  = y_max_real * 1000

%% Verificacion del criterio L/250 (deflexion admisible)
% Para vigas de piso el limite tipico es L/250.

L_sobre_250 = L_num * 1000 / 250
%   Si y_max_mm < L_sobre_250 -> CUMPLE
%   Si y_max_mm > L_sobre_250 -> NO CUMPLE
