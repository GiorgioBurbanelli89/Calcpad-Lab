%% Test de integral doble simbolica
syms x y

% Caso 1: int(int(x*y, x, 0, 1), y, 0, 1) = 1/4
int_inner = int(x*y, x, 0, 1)        % primero integrar respecto a x
int_outer = int(int_inner, y, 0, 1)  % luego respecto a y

% Caso 2: integral 1D que da expresion simbolica
F1 = int(x^2 + y, x)                  % integral parcial respecto a x

% Caso 3: doble integral anidada en una sola linea
result_2d = int(int(x*y, x, 0, 1), y, 0, 1)
