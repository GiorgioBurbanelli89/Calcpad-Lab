% Surface_3D.m — Superficie 3D con surf()
clear; clc;

[X, Y] = meshgrid(-3:0.15:3, -3:0.15:3);
Z = peaks(X, Y);  % funcion built-in para demo

figure;
surf(X, Y, Z);
colorbar;
xlabel('x'); ylabel('y'); zlabel('z');
title('Superficie peaks(x,y)');
shading interp;

fprintf('Superficie 3D peaks renderizada con sombreado interpolado.\n');
