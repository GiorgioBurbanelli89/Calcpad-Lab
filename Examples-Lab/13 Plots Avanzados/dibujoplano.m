function dibujoplano(X,Y,NI,NJ)
%
% Programa para dibujar una estructura plana
%
% Por: Roberto Aguiar Falconi
%           CEINCI-ESPE
%         Septiembre de 2009  (adaptado a Hekatan Lab)
%-------------------------------------------------------------
% dibujoplano (X,Y,NI,NJ)
%-------------------------------------------------------------
% X        Vector que contiene coordenadas en X
% Y        Vector que contiene coordenadas en Y
% NI       Vector con los nudos iniciales de los elementos
% NJ       Vector con los nudos finales de los elementos

if nargin < 4
    % Datos de ejemplo: portico plano de 2 vanos y 1 piso
    X  = [0 4 8 0 4 8];
    Y  = [0 0 0 3 3 3];
    NI = [1 2 3 4 5];
    NJ = [4 5 6 5 6];
end

figure; hold on;
nel = length(NI);
for e = 1:nel
    xi = X(NI(e)); yi = Y(NI(e));
    xj = X(NJ(e)); yj = Y(NJ(e));
    plot([xi xj], [yi yj], 'b-', 'LineWidth', 2);
end

% Numero de cada elemento en el punto medio (rojo)
xm = (X(NI) + X(NJ)) / 2;
ym = (Y(NI) + Y(NJ)) / 2;
text(xm, ym, 1:nel, 'Color', 'red', 'FontSize', 10);

% Nudos y su numeracion (negro)
plot(X, Y, 'ko', 'MarkerFaceColor', 'k');
text(X, Y, 1:length(X), 'Color', 'black', 'FontSize', 10);

hold off; axis equal;
title('Numeracion de nudos y elementos');
end
