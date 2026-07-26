function [K3] = kmiembro_nudo_rigido_compuesta(Area, Inercia, c1, c2, Lon, E, sen, cose, beta,fa,v)
% Matriz de rigidez de un elemento en coordenadas globales
%-------------------------------------------------------------
% [K3] = kmiembro_nudo_rigido_compuesta()
%-------------------------------------------------------------
% Area: Area de la sección transversal.
% Inercia: Momento de inercia de la sección transversal.
% c1: Longitud de nudo rígido del Nudo Inicial.
% c2: Longitud de nudo rígido del Nudo Final.
% Lon: Longitud del elemento.
% sen: Seno del ángulo para pasar de local a global.
% coseno: Coseno del ángulo para pasar de local a global.
% VC: Vector de colocación de elementos.
% E: Módulo de elasticidad del material.
% fa: Factor de ajuste.
% beta: Factor de corrección.

% Constantes
%v = 0.30;  % Relación de Poisson
G = E / (2 * (1 + v));  % Módulo de rigidez
Iagr = fa * Inercia;  % Inercia ajustada

% Coeficiente de rigidez
fi = (3 * E * Iagr * beta) / (G * Area * Lon^2);
kf = (4 * E * Iagr * (1 + fi)) / (Lon * (1 + 4 * fi));
a = (2 * E * Iagr * (1 - 2 * fi)) / (Lon * (1 + 4 * fi));
b = (kf + a) / Lon;
t = 2 * b / Lon;
r = E * Area / Lon;

% Matriz de rigidez en coordenadas globales
if sen == 0 % Caso de viga
    K3 = [0, 0, 0, 0, 0, 0;
          0, t, b + c1 * t, 0, -t, b + c2 * t;
          0, b + c1 * t, kf + 2 * c1 * b + c1^2 * t, 0, -(b + c1 * t), a + c1 * b + c2 * b + c1 * c2 * t;
          0, 0, 0, 0, 0, 0;
          0, -t, -(b + c1 * t), 0, t, -(b + c2 * t);
          0, b + c2 * t, a + c1 * b + c2 * b + c1 * c2 * t, 0, -(b + c2 * t), kf + 2 * c2 * b + c2^2 * t];
else % Caso general
    K3 = [t, 0, -(b + c1 * t), -t, 0, -(b + c2 * t);
          0, r, 0, 0, -r, 0;
          -(b + c1 * t), 0, kf + 2 * c1 * b + c1^2 * t, b + c1 * t, 0, a + c1 * b + c2 * b + c1 * c2 * t;
          -t, 0, b + c1 * t, t, 0, b + c2 * t;
          0, -r, 0, 0, r, 0;
          -(b + c2 * t), 0, a + c1 * b + c2 * b + c1 * c2 * t, b + c2 * t, 0, kf + 2 * c2 * b + c2^2 * t];
end
% K3=K3/1000 %ton/cm
format short G;
return
