% Test de graph() — igual en MATLAB 2017a y Hekatan Lab
% Portico de 2 vanos y 2 pisos: 9 nudos, 12 elementos
clear all; close all;
X = [0 4 8  0 4 8  0 4 8];
Y = [0 0 0  3 3 3  6 6 6];
NI = [1 2 4 5 7 8  1 2 3 4 5 6];
NJ = [2 3 5 6 8 9  4 5 6 7 8 9];

weights = zeros(1, length(NI));   % preasignar: no heredar del workspace
for i = 1:length(NI)
    weights(i) = i;
end

disp('--- graph() y su tabla Edges ---')
G = graph(NI, NJ, weights);
w = G.Edges.Weight;
fprintf('elementos    = %d\n', length(NI));
fprintf('pesos 1..5   = %d %d %d %d %d\n', w(1), w(2), w(3), w(4), w(5));
fprintf('suma pesos   = %d\n', sum(w));

figure
plot(G, 'XData', X, 'YData', Y, 'EdgeLabel', G.Edges.Weight, 'linewidth', 2)
title('Numeracion de nudos y elementos')
