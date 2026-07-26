% Matrices y vectores SIMBOLICOS en Hekatan Lab
% (codigo MATLAB nativo, fuera de %, valida contra MATLAB 2017a)

%% Vector simbolico
syms k
v = k * [1 2 3]

%% Matriz simbolica 2x2
syms a b
M = [a, b; b, a]

%% Matriz constitutiva D de placa (Kirchhoff), simbolica
syms E nu t
D = E*t^3/(12*(1-nu^2)) * [1, nu, 0; nu, 1, 0; 0, 0, (1-nu)/2]
