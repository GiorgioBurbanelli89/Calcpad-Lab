%% Portico dibujado con graph() — nudos y elementos numerados
% Patron de CEINCI-LAB: una arista por barra, el peso es el numero de elemento.
clear all; close all;

% portico de 3 vanos y 3 pisos
nv = 3; np = 3; L = 5; h = 3;
X = []; Y = [];
for j = 0:np
    for i = 0:nv
        X(end+1) = i*L;  Y(end+1) = j*h;
    end
end
nx = nv + 1;

NI = []; NJ = [];
for j = 0:np-1                      % columnas
    for i = 1:nx
        NI(end+1) = j*nx + i;  NJ(end+1) = (j+1)*nx + i;
    end
end
for j = 1:np                        % vigas
    for i = 1:nv
        NI(end+1) = j*nx + i;  NJ(end+1) = j*nx + i + 1;
    end
end

weights = zeros(1, length(NI));   % preasignar: no heredar del workspace
for i = 1:length(NI), weights(i) = i; end
fprintf('nudos = %d, elementos = %d\n', length(X), length(NI));

G = graph(NI, NJ, weights);
figure
plot(G, 'XData', X, 'YData', Y, 'EdgeLabel', G.Edges.Weight, 'linewidth', 2)
title('Portico 3x3 - numeracion de nudos y elementos')
