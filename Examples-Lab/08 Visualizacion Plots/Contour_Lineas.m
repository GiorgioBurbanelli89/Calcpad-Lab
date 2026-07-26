% Contour_Lineas.m — Mapa de contornos
clear; clc;

[X, Y] = meshgrid(-3:0.1:3, -3:0.1:3);
Z = X.^2 + Y.^2 - 2*X.*Y;

figure;
contour(X, Y, Z, 20);
xlabel('x'); ylabel('y');
title('Contornos de f(x,y) = x^2 + y^2 - 2xy');
grid on;
axis equal;

fprintf('Mapa de contornos generado con 20 niveles.\n');
