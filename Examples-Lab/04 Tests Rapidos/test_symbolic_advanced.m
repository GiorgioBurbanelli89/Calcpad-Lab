%% Deduccion simbolica de viga simplemente apoyada con carga distribuida
% Viga de luz L con carga uniforme q sobre toda la luz.
% Apoyos: simple en x=0 y x=L (sin restricciones a rotacion).
%
% Demuestra:
%   - Declarar variables simbolicas (syms)
%   - Integrar expresiones (cortante -> momento)
%   - Evaluar en puntos especificos (subs)
%   - Comparar con formula cerrada conocida

syms x q L E I

%% Cortante en funcion de x: V(x) = q*L/2 - q*x
V = q*L/2 - q*x

%% Momento por integracion del cortante (M(0) = 0 en apoyo)
M = int(V, x)

%% Momento maximo en el centro (x = L/2)
M_max = subs(M, x, L/2)

%% Comparacion con formula cerrada: M_max = q*L^2/8
M_max_teorico = q*L^2/8

%% Pendiente y deflexion (EI*y'' = -M)
% Integrar dos veces para obtener y(x). Constantes de integracion se
% determinan por las condiciones de borde y(0)=0, y(L)=0.

theta = int(-M, x) / (E*I)
y = int(theta, x)

%% Deflexion teorica maxima (en centro): y_max = 5*q*L^4 / (384*E*I)
y_max_teorico = 5*q*L^4 / (384*E*I)

%% Sustitucion numerica de ejemplo: q=10 kN/m, L=6 m, E=210 GPa, I=8346 cm^4
% Convertir todo a sistema consistente N-m
q_num = 10000;          % 10 kN/m = 10000 N/m
L_num = 6;              % 6 m
E_num = 210e9;          % 210 GPa en Pa
I_num = 8346.265e-8;    % 8346 cm^4 en m^4

M_num = subs(M_max_teorico, {q, L}, {q_num, L_num})
y_num = subs(y_max_teorico, {q, L, E, I}, {q_num, L_num, E_num, I_num})
