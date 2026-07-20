%% API del objeto graph — igual en MATLAB 2017a y Hekatan Lab
clear all; close all;
NI = [1 2 3 1];  NJ = [2 3 4 4];  w = [10 20 30 40];
G = graph(NI, NJ, w);

fprintf('numnodes  = %d        (esperado 4)\n', numnodes(G));
fprintf('numedges  = %d        (esperado 4)\n', numedges(G));

d = degree(G);
fprintf('degree    = %d %d %d %d  (esperado 2 2 2 2)\n', d(1), d(2), d(3), d(4));
fprintf('degree(1) = %d        (esperado 2)\n', degree(G,1));

nb = neighbors(G,1);
fprintf('neighbors(1) = %d %d   (esperado 2 4)\n', nb(1), nb(2));

A = full(adjacency(G));   %% MATLAB la devuelve DISPERSA: full() para poder imprimirla
fprintf('adjacency: A(1,2)=%d A(1,4)=%d A(1,3)=%d  (esperado 1 1 0)\n', ...
        A(1,2), A(1,4), A(1,3));

c = conncomp(G);
fprintf('conncomp  = %d %d %d %d  (todo conectado = 1 1 1 1)\n', c(1),c(2),c(3),c(4));

p = shortestpath(G, 1, 3);
fprintf('shortestpath(1,3) = %s   (por pesos: 1-4-3)\n', mat2str(p));

try
    error('probando el catch');
catch e
    fprintf('catch e.message = %s\n', e.message);
end

%% El mismo grafo dibujado
X = [0 3 6 3];  Y = [0 0 0 3];
figure
plot(G, 'XData', X, 'YData', Y, 'EdgeLabel', G.Edges.Weight, 'linewidth', 2)
title('El grafo del test - nudos y pesos de cada arista')
