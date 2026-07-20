%% graph() — la tabla Edges
% Verifica que G.Edges.Weight y G.Edges.EndNodes se lean como en MATLAB.
% Asi es como CEINCI-LAB numera los elementos en dibujoplano.m
clear all; close all;

NI = [1 2 3 1];
NJ = [2 3 4 4];
w  = [10 20 30 40];

G = graph(NI, NJ, w);

disp('--- G.Edges.Weight ---')
pesos = G.Edges.Weight

disp('--- G.Edges.EndNodes (columna 1 = nudo inicial, columna 2 = final) ---')
extremos = G.Edges.EndNodes

fprintf('\n');
fprintf('numero de elementos : %d\n', length(pesos));
fprintf('suma de pesos       : %d   (esperado 100)\n', sum(pesos));
fprintf('peso maximo         : %d   (esperado 40)\n', max(pesos));
